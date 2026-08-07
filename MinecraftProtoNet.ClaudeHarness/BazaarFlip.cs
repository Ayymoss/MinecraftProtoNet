using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>One flip candidate as BazaarCompanion scores it. Mirrors the API's FlipOpportunity record.</summary>
public sealed record FlipOpportunity(
    [property: JsonPropertyName("productKey")] string ProductKey,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("bestBidPrice")] double BestBidPrice,
    [property: JsonPropertyName("bestAskPrice")] double BestAskPrice,
    [property: JsonPropertyName("bidWeekVolume")] double BidWeekVolume,
    [property: JsonPropertyName("askWeekVolume")] double AskWeekVolume,
    [property: JsonPropertyName("spreadPercent")] double SpreadPercent,
    [property: JsonPropertyName("estimatedProfitPerUnit")] double EstimatedProfitPerUnit,
    [property: JsonPropertyName("opportunityScore")] double OpportunityScore,
    [property: JsonPropertyName("isManipulated")] bool IsManipulated,
    [property: JsonPropertyName("unstackable")] bool Unstackable);

public sealed record FlipOptions(
    string Server,
    int Port,
    int HubNumber,
    int Quantity,
    double MaxUnitPrice,
    string? ForceProduct,
    int MonitorMinutes,
    int PollSeconds);

/// <summary>
/// End-to-end Bazaar flip driven by BazaarCompanion: ask the API what is worth flipping, place a buy order at
/// the top of the book, wait for it to fill, claim the goods, offer them back at the top of the sell side, wait
/// for that to fill, and claim the coins.
///
/// The waits are the point — orders priced inside the spread do not fill instantly, so the bot stays connected
/// and re-opens the order manager on a timer rather than the run being a one-shot chain.
///
/// Money is real. Every coin-spending click is deliberate and logged with the price it was quoted at.
/// </summary>
public sealed class BazaarFlipTask(BazaarSession session, HttpClient api, Action<string> log)
{
    /// <summary>Hypixel's Bazaar tax on sell proceeds — matches BazaarCompanion's own constant.</summary>
    private const double TaxRate = 0.01125;

    private static readonly (int X, int Y, int Z) HubSelectorPos = (-5, 69, -22);
    private static readonly (int X, int Y, int Z) BazaarPos = (-36, 72, -28);

    public async Task<bool> RunAsync(FlipOptions options)
    {
        session.Subscribe();
        try
        {
            if (!await session.ConnectAndSpawnAsync(options.Server, options.Port))
            {
                log("CONNECT/SPAWN FAILED");
                return false;
            }
            log("connected + spawned");
            await session.SelectEmptyHotbarSlotAsync();

            await session.SendCommandAsync("skyblock");
            await Task.Delay(TimeSpan.FromSeconds(9));
            await session.SendCommandAsync("hub");
            await Task.Delay(TimeSpan.FromSeconds(6));

            if (!await GoToHubAsync(options.HubNumber)) return false;

            // Pick the product BEFORE walking: if the API has nothing tradable there is no point being here.
            var flip = await ChooseFlipAsync(options);
            if (flip is null) return false;

            if (!await WalkToBazaarAsync()) return false;

            var placed = await PlaceBuyOrderAsync(flip, options.Quantity);
            if (placed is null) return false;

            var bought = await WaitAndClaimAsync($"BUY {flip.Name}", "items to claim", options);
            if (!bought)
            {
                log($"buy order for {flip.Name} did not fill within {options.MonitorMinutes} minutes — " +
                    "it is still live on the Bazaar and can be claimed or cancelled later");
                return false;
            }

            var offered = await PlaceSellOfferAsync(flip);
            if (offered is null) return false;

            var sold = await WaitAndClaimAsync($"SELL {flip.Name}", "coins to claim", options);
            if (!sold)
            {
                log($"sell offer for {flip.Name} did not fill within {options.MonitorMinutes} minutes — " +
                    "it is still live and holds the goods");
                return false;
            }

            var profit = offered.Value.Proceeds - placed.Value.Cost;
            log("=========================================================");
            log($"FLIP COMPLETE — {flip.Name} x{options.Quantity}");
            log($"  bought {options.Quantity}x @ {placed.Value.UnitPrice} = {placed.Value.Cost:F1} coins");
            log($"  sold   {options.Quantity}x @ {offered.Value.UnitPrice} = {offered.Value.Proceeds:F1} coins (net of {TaxRate:P2} tax)");
            log($"  PROFIT = {profit:F1} coins");
            log("=========================================================");
            return profit > 0;
        }
        finally
        {
            session.Unsubscribe();
            try { await session.DisconnectAsync(); } catch { /* best-effort */ }
            log("disconnected");
        }
    }

    // ===== Hub =====

    /// <summary>
    /// Walks to the Hub Selector and switches to the requested hub, so a human can follow the bot. The hub the
    /// account is already in is marked with red terracotta rather than quartz; clicking it is pointless, so
    /// that case just carries on.
    /// </summary>
    private async Task<bool> GoToHubAsync(int hubNumber)
    {
        log($"--- going to Hub #{hubNumber} ---");
        if (!await session.FindNpcAsync("Hub Selector", TimeSpan.FromSeconds(20)))
        {
            log("Hub Selector not found");
            return false;
        }

        if (!await session.WalkToAsync(HubSelectorPos, 90)) return false;
        await session.ApproachNpcAsync();
        if (!await session.OpenNpcMenuAsync("Hub Selector"))
        {
            log("hub selector menu did not open");
            session.LogMenu();
            return false;
        }

        var wanted = $"SkyBlock Hub #{hubNumber}";
        var slot = session.FindSlot(wanted);
        if (slot is null)
        {
            log($"\"{wanted}\" is not in \"{session.ContainerTitle}\"");
            session.LogMenu();
            return false;
        }

        if (slot.Item.Contains("red_terracotta", StringComparison.OrdinalIgnoreCase))
        {
            log($"already in Hub #{hubNumber} — no switch needed");
            await session.CloseAsync();
            return true;
        }

        var chatMark = session.ChatCount;
        await session.ClickAsync(wanted);

        // The switch is a full server change: Start Configuration, a new Login, then a fresh spawn.
        log("waiting for the hub switch to land");
        await Task.Delay(TimeSpan.FromSeconds(12));
        await session.SelectEmptyHotbarSlotAsync();
        foreach (var line in session.ChatSince(chatMark).Where(l => l.Contains("Hub", StringComparison.OrdinalIgnoreCase)))
        {
            log($"  {line}");
        }
        return true;
    }

    private async Task<bool> WalkToBazaarAsync()
    {
        log("--- walking to the Bazaar ---");
        if (!await session.FindNpcAsync("Bazaar", TimeSpan.FromSeconds(20)))
        {
            // Entities only stream in near the destination, so a miss at spawn is expected — walk first.
            if (!await session.WalkToAsync(BazaarPos, 120)) return false;
            if (!await session.FindNpcAsync("Bazaar", TimeSpan.FromSeconds(20)))
            {
                log("Bazaar NPC not found");
                return false;
            }
        }
        else if (!await session.WalkToAsync(BazaarPos, 120))
        {
            return false;
        }

        await session.ApproachNpcAsync();
        return true;
    }

    // ===== Choosing what to flip =====

    private async Task<FlipOpportunity?> ChooseFlipAsync(FlipOptions options)
    {
        log("--- asking BazaarCompanion what to flip ---");
        var url = $"/api/bot/flips?maxResults=40&minScore=2.5&maxPrice={options.MaxUnitPrice:F0}";
        List<FlipOpportunity>? flips;
        try
        {
            flips = await api.GetFromJsonAsync<List<FlipOpportunity>>(url);
        }
        catch (Exception ex)
        {
            log($"flips request failed: {ex.Message}");
            return null;
        }

        if (flips is null || flips.Count == 0)
        {
            log("the API returned no flip opportunities");
            return null;
        }

        // Liquidity first, not headline margin: a 600% spread on a product that trades twice a day never
        // fills, and an unfilled order is not a profit. Both sides have to be busy because both sides have to
        // fill — the buy AND the sell.
        var ranked = flips
            .Where(f => !f.IsManipulated && !f.Unstackable)
            .Where(f => f.BestAskPrice <= options.MaxUnitPrice)
            .Where(f => f.EstimatedProfitPerUnit > 0)
            .OrderByDescending(f => Math.Min(f.BidWeekVolume, f.AskWeekVolume))
            .ToList();

        log($"{flips.Count} candidates, {ranked.Count} tradable; top by two-sided liquidity:");
        foreach (var f in ranked.Take(5))
        {
            log($"  {f.Name,-28} bid {f.BestBidPrice,9:F1} ask {f.BestAskPrice,9:F1} " +
                $"spread {f.SpreadPercent,6:F1}% profit/u {f.EstimatedProfitPerUnit,8:F1} " +
                $"vol/wk {Math.Min(f.BidWeekVolume, f.AskWeekVolume),12:N0}");
        }

        var chosen = options.ForceProduct is { Length: > 0 } forced
            ? ranked.FirstOrDefault(f => f.Name.Contains(forced, StringComparison.OrdinalIgnoreCase))
              ?? flips.FirstOrDefault(f => f.Name.Contains(forced, StringComparison.OrdinalIgnoreCase))
            : ranked.FirstOrDefault();

        if (chosen is null)
        {
            log("no candidate passed the filters");
            return null;
        }

        log($"chosen: {chosen.Name} (score {chosen.OpportunityScore:F1})");
        return chosen;
    }

    // ===== Trading =====

    /// <summary>Opens the product page for a product by name, via the Bazaar's own search.</summary>
    private async Task<bool> OpenProductAsync(string productName)
    {
        if (!await session.OpenNpcMenuAsync("Bazaar"))
        {
            log("Bazaar menu did not open");
            session.LogMenu();
            return false;
        }

        if (!await session.ClickAsync("Search")) return false;
        if (!await session.SignAsync(productName)) return false;
        if (!await session.ClickAsync(productName)) return false;

        if (!session.ContainerTitle.Contains(productName, StringComparison.OrdinalIgnoreCase))
        {
            log($"expected the {productName} product page, got \"{session.ContainerTitle}\"");
            return false;
        }
        return true;
    }

    private async Task<(double UnitPrice, double Cost)?> PlaceBuyOrderAsync(FlipOpportunity flip, int quantity)
    {
        log($"--- placing a buy order: {quantity}x {flip.Name} ---");
        if (!await OpenProductAsync(flip.Name)) return null;

        if (session.BestBid is not { } bid || session.BestAsk is not { } ask)
        {
            log("could not read the order book off the product page");
            return null;
        }

        // Sit one tick above the best buy order so we are first in the queue, and confirm the flip still pays
        // after tax at the prices the game is quoting right now rather than the ones the API cached.
        var buyPrice = Math.Round(bid + 0.1, 1);
        var sellPrice = Math.Round(ask - 0.1, 1);
        var margin = sellPrice * (1 - TaxRate) - buyPrice;
        log($"live book: bid {bid} / ask {ask} -> buy at {buyPrice}, plan to sell at {sellPrice}, " +
            $"margin {margin:F1}/unit ({margin * quantity:F1} total)");

        if (margin <= 0)
        {
            log("the spread does not cover the tax at live prices — not trading");
            return null;
        }

        if (!await session.ClickAsync("Create Buy Order")) return null;
        if (!await session.ClickAsync("Custom Amount")) return null;
        if (!await session.SignAsync(quantity.ToString())) return null;
        if (!await session.ClickAsync("Custom Price")) return null;
        if (!await session.SignAsync(buyPrice.ToString("0.#"))) return null;

        var confirm = session.FindSlot("Buy Order");
        if (confirm is null || !session.ContainerTitle.Contains("Confirm", StringComparison.OrdinalIgnoreCase))
        {
            log($"expected the buy-order confirmation, got \"{session.ContainerTitle}\"");
            return null;
        }
        log($"confirming: {string.Join(" / ", confirm.Lore.Where(l => l.Trim().Length > 0))}");

        var chatMark = session.ChatCount;
        await session.ClickAsync("Buy Order", waitForChange: false);
        await Task.Delay(3000);

        var setup = session.ChatSince(chatMark).FirstOrDefault(l => l.Contains("Buy Order Setup", StringComparison.OrdinalIgnoreCase));
        if (setup is null)
        {
            log("no confirmation in chat — assuming the order was NOT placed");
            foreach (var line in session.ChatSince(chatMark)) log($"  chat: {line}");
            return null;
        }

        log($"ORDER PLACED: {setup}");
        await session.CloseAsync();
        return (buyPrice, buyPrice * quantity);
    }

    private async Task<(double UnitPrice, double Proceeds)?> PlaceSellOfferAsync(FlipOpportunity flip)
    {
        log($"--- placing a sell offer for {flip.Name} ---");
        if (!await OpenProductAsync(flip.Name)) return null;

        if (session.BestAsk is not { } ask)
        {
            log("could not read the ask off the product page");
            return null;
        }

        var sellPrice = Math.Round(ask - 0.1, 1);
        log($"live ask {ask} -> offering at {sellPrice}");

        // No amount screen on this side: a sell offer covers everything of that product you are holding.
        if (!await session.ClickAsync("Create Sell Offer")) return null;
        if (!await session.ClickAsync("Custom Price")) return null;
        if (!await session.SignAsync(sellPrice.ToString("0.#"))) return null;

        var confirm = session.FindSlot("Sell Offer");
        if (confirm is null || !session.ContainerTitle.Contains("Confirm", StringComparison.OrdinalIgnoreCase))
        {
            log($"expected the sell-offer confirmation, got \"{session.ContainerTitle}\"");
            return null;
        }
        log($"confirming: {string.Join(" / ", confirm.Lore.Where(l => l.Trim().Length > 0))}");

        // "You earn: N coins" is the post-tax figure, which is the number that decides whether this was profitable.
        var proceeds = ParseCoins(confirm.Lore.FirstOrDefault(l => l.Contains("You earn", StringComparison.OrdinalIgnoreCase)));

        var chatMark = session.ChatCount;
        await session.ClickAsync("Sell Offer", waitForChange: false);
        await Task.Delay(3000);

        var setup = session.ChatSince(chatMark).FirstOrDefault(l => l.Contains("Sell Offer Setup", StringComparison.OrdinalIgnoreCase));
        if (setup is null)
        {
            log("no confirmation in chat — assuming the offer was NOT placed");
            foreach (var line in session.ChatSince(chatMark)) log($"  chat: {line}");
            return null;
        }

        log($"OFFER PLACED: {setup}");
        await session.CloseAsync();
        return (sellPrice, proceeds ?? 0);
    }

    // ===== Waiting =====

    /// <summary>
    /// Re-opens the order manager every poll until the named order has something to collect, then claims it.
    /// This is the part that needs the bot to stay in the world: an order priced inside the spread fills when
    /// somebody else trades into it, which can be seconds or can be many minutes.
    /// </summary>
    private async Task<bool> WaitAndClaimAsync(string orderName, string claimPhrase, FlipOptions options)
    {
        log($"--- waiting for \"{orderName}\" ({claimPhrase}), up to {options.MonitorMinutes} min ---");
        var deadline = DateTime.UtcNow.AddMinutes(options.MonitorMinutes);
        var poll = 0;

        while (DateTime.UtcNow < deadline)
        {
            poll++;
            if (!session.Client.IsConnected)
            {
                log("disconnected while waiting");
                return false;
            }

            if (!await session.OpenNpcMenuAsync("Bazaar") || !await session.ClickAsync("Manage Orders"))
            {
                log($"poll {poll}: could not open the order manager, retrying");
                await session.CloseAsync();
                await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds));
                continue;
            }

            var order = session.FindSlot(orderName);
            if (order is null)
            {
                log($"poll {poll}: \"{orderName}\" is no longer listed — treating it as settled");
                await session.CloseAsync();
                return true;
            }

            var status = order.Lore.FirstOrDefault(l => l.Contains("Filled:", StringComparison.OrdinalIgnoreCase)) ?? "no fill line";
            var claimable = order.Lore.Any(l => l.Contains(claimPhrase, StringComparison.OrdinalIgnoreCase));
            log($"poll {poll} ({DateTime.Now:HH:mm:ss}): {status}{(claimable ? "  <- claimable" : "")}");

            if (claimable)
            {
                var chatMark = session.ChatCount;
                await session.ClickAsync(orderName);
                await Task.Delay(2000);
                foreach (var line in session.ChatSince(chatMark).Where(l => l.Contains("Claim", StringComparison.OrdinalIgnoreCase)))
                {
                    log($"  {line}");
                }
                await session.CloseAsync();
                return true;
            }

            await session.CloseAsync();
            await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds));
        }

        log($"gave up waiting for \"{orderName}\"");
        return false;
    }

    private static double? ParseCoins(string? loreLine)
    {
        if (loreLine is null) return null;
        var match = System.Text.RegularExpressions.Regex.Match(loreLine, @"([\d,]+(?:\.\d+)?)\s*coins");
        if (!match.Success) return null;
        return double.TryParse(match.Groups[1].Value.Replace(",", ""),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
