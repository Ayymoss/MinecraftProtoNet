using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>
/// Trading task: find a villager, walk to it, open its trade menu, obtain whatever that trade costs, execute
/// it, and end up holding the result.
///
/// This is a different shape of test to the movement courses — it exercises entity interaction, the merchant
/// container, and inventory handling rather than pathing. The bot is NOT pre-stocked: the whole point is that
/// it reads the offer off the villager and only then are the required items spawned in, so the trade must be
/// discovered rather than assumed.
/// </summary>
public sealed class VillagerTask(
    IMinecraftClient client,
    IBaritone baritone,
    IContainerManager containers,
    IItemRegistryService items,
    Func<string, Task> sendCommand)
{
    private static void Log(string msg) => Console.WriteLine($"[villager] {msg}");

    /// <summary>Merchant menu layout: 0 = cost A, 1 = cost B, 2 = result.</summary>
    private const short ResultSlot = 2;

    public async Task<(bool Success, string Detail)> RunAsync(string wantedItem, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        var villager = await FindVillagerAsync(deadline);
        if (villager == null) return (false, "no villager found near the start");
        Log($"found villager id={villager.EntityId} at ({villager.Position.X:F1},{villager.Position.Y:F1},{villager.Position.Z:F1})");

        if (!await WalkToAsync(villager, deadline)) return (false, "could not reach the villager");

        var offers = await OpenTradeMenuAsync(villager, deadline);
        if (offers == null) return (false, "trade menu did not open / no offers received");

        Log($"villager has {offers.Count} offer(s):");
        for (var i = 0; i < offers.Count; i++)
        {
            Log($"  [{i}] {Describe(offers[i])}");
        }

        var index = offers.FindIndex(o => NameOf(o.Result.ItemId ?? 0) == wantedItem && !o.IsOutOfStock);
        if (index < 0) return (false, $"no in-stock offer yields {wantedItem}");
        var offer = offers[index];
        Log($"selected offer [{index}]: {Describe(offer)}");

        // "Spawn the needed item" — give exactly what this offer costs, discovered from the offer itself.
        await GiveCostAsync(offer.BaseCostA);
        if (offer.CostB is { Count: > 0 } costB) await GiveCostAsync(costB);
        await Task.Delay(1500); // let the give land and the inventory sync

        if (!await ExecuteTradeAsync(index, deadline)) return (false, "trade did not complete");

        var held = CountOf(wantedItem);
        return held > 0
            ? (true, $"acquired {held}x {wantedItem} from trade [{index}]")
            : (false, $"trade executed but no {wantedItem} in inventory");
    }

    private async Task<WorldEntity?> FindVillagerAsync(DateTime deadline)
    {
        // Entities stream in after the join; poll rather than assuming they are present immediately.
        while (DateTime.UtcNow < deadline)
        {
            var self = client.State.LocalPlayer?.Entity?.Position;
            if (self != null)
            {
                var nearest = client.State.WorldEntities.GetAllEntities()
                    .Where(e => e.EntityType == EntityTypes.Villager)
                    .OrderBy(e => Dist(e.Position, self))
                    .FirstOrDefault();
                if (nearest != null) return nearest;
            }
            await Task.Delay(250);
        }
        return null;
    }

    private async Task<bool> WalkToAsync(WorldEntity villager, DateTime deadline)
    {
        var target = villager.Position;
        // GoalNear rather than GoalBlock: standing ON the villager is neither possible nor required, and the
        // villager drifts, so an exact block goal would never be satisfied.
        baritone.GetCustomGoalProcess().SetGoalAndPath(
            new GoalNear((int)Math.Floor(target.X), (int)Math.Floor(target.Y), (int)Math.Floor(target.Z), 2));
        Log("pathing to the villager...");

        while (DateTime.UtcNow < deadline)
        {
            var self = client.State.LocalPlayer?.Entity?.Position;
            var current = client.State.WorldEntities.GetEntity(villager.EntityId)?.Position ?? target;
            if (self != null && Dist(current, self) <= 3.5)
            {
                Log($"arrived, {Dist(current, self):F1} blocks from the villager");
                baritone.GetPathingBehavior().CancelEverything();
                return true;
            }
            await Task.Delay(200);
        }
        return false;
    }

    private async Task<List<MerchantOffer>?> OpenTradeMenuAsync(WorldEntity villager, DateTime deadline)
    {
        for (var attempt = 1; attempt <= 3 && DateTime.UtcNow < deadline; attempt++)
        {
            Log($"interacting with the villager (attempt {attempt})");
            await containers.InteractWithEntityAsync(villager.EntityId);

            var waitUntil = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < waitUntil)
            {
                var offers = containers.CurrentContainer?.MerchantData?.Offers;
                if (offers is { Count: > 0 }) return offers.ToList();
                await Task.Delay(200);
            }
            Log("no offers yet; retrying");
        }
        return null;
    }

    private async Task GiveCostAsync(ItemCost cost)
    {
        var name = NameOf(cost.ItemId);
        Log($"spawning trade cost: {cost.Count}x {name}");
        await sendCommand($"give @s {name} {cost.Count}");
    }

    private async Task<bool> ExecuteTradeAsync(int index, DateTime deadline)
    {
        Log($"selecting trade {index}");
        await containers.SelectTradeAsync(index);
        await Task.Delay(750);

        var container = containers.CurrentContainer;
        if (container == null) return false;

        // Selecting a trade only stages the result; it has to be taken out of the result slot.
        Log("taking the result out of the merchant result slot");
        await containers.QuickMoveSlotAsync(ResultSlot, container.ContainerId);
        await Task.Delay(750);

        await containers.CloseContainerAsync();

        while (DateTime.UtcNow < deadline)
        {
            if (CountOf("minecraft:emerald") > 0) return true;
            await Task.Delay(200);
        }
        return true; // let the caller's inventory check decide
    }

    private int CountOf(string itemName)
    {
        var inv = client.State.LocalPlayer?.Entity?.Inventory;
        if (inv == null) return 0;
        return inv.Items
            .Where(kv => kv.Value.ItemId is > 0 && NameOf(kv.Value.ItemId ?? 0) == itemName)
            .Sum(kv => kv.Value.ItemCount);
    }

    private string NameOf(int itemId) => items.GetItemName(itemId) ?? $"item:{itemId}";

    private string Describe(MerchantOffer o)
    {
        var b = o.CostB is { Count: > 0 } cb ? $" + {cb.Count}x {NameOf(cb.ItemId)}" : "";
        var stock = o.IsOutOfStock ? " (OUT OF STOCK)" : "";
        return $"{o.BaseCostA.Count}x {NameOf(o.BaseCostA.ItemId)}{b} -> {o.Result.ItemCount}x {NameOf(o.Result.ItemId ?? 0)}{stock}";
    }

    private static double Dist(Core.Models.Core.Vector3<double> a, Core.Models.Core.Vector3<double> b)
        => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
}
