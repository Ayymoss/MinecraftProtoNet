using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>
/// Everything needed to stand in front of a Hypixel NPC and drive its menus: join, walk, right-click, click
/// slots by name, answer sign prompts, and read the menu back.
///
/// Split out of the menu-recon tool so the same navigation drives both recon (map the UI) and trading (use it).
/// It knows nothing about WHY a slot is being clicked — the caller decides that, including whether a click is
/// allowed to spend coins.
/// </summary>
public sealed class BazaarSession(
    IMinecraftClient client,
    IChatEventBus chatBus,
    ISignEventBus signBus,
    IBaritoneProvider baritoneProvider,
    IContainerManager containers,
    IItemRegistryService items,
    Action<string> log)
{
    private readonly List<string> _chatLog = [];
    private readonly System.Collections.Concurrent.ConcurrentQueue<SignEditorEventArgs> _signPrompts = new();
    private WorldEntity? _npc;
    private bool _subscribed;

    private static readonly System.Text.RegularExpressions.Regex PriceLine =
        new(@"-\s*([\d,]+(?:\.\d+)?)\s*coins each", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Lowest standing sell offer, as of the last product page read.</summary>
    public double? BestAsk { get; private set; }

    /// <summary>Highest standing buy order, as of the last product page read.</summary>
    public double? BestBid { get; private set; }

    public IMinecraftClient Client => client;
    public ContainerState? Container => containers.CurrentContainer;
    public string ContainerTitle => Container is null ? "<none>" : ItemTextHelper.StripFormattingCodes(Container.Title);

    public List<string> ChatSnapshot()
    {
        lock (_chatLog) return [.. _chatLog];
    }

    /// <summary>Chat lines added since the given index — how a caller sees what one action produced.</summary>
    public List<string> ChatSince(int index)
    {
        lock (_chatLog) return index >= _chatLog.Count ? [] : _chatLog[index..];
    }

    public int ChatCount
    {
        get { lock (_chatLog) return _chatLog.Count; }
    }

    public void Subscribe()
    {
        if (_subscribed) return;
        chatBus.OnSystemChat += OnChat;
        signBus.OnSignEditorOpened += OnSign;
        _subscribed = true;
    }

    public void Unsubscribe()
    {
        if (!_subscribed) return;
        chatBus.OnSystemChat -= OnChat;
        signBus.OnSignEditorOpened -= OnSign;
        _subscribed = false;
    }

    private void OnChat(SystemChatEventArgs e)
    {
        var line = ItemTextHelper.StripFormattingCodes(string.Join("", e.TextParts)).Trim();
        if (line.Length == 0) return;

        // The action bar repeats health/mana every tick; keeping it would bury everything else.
        if (line.Contains("Mana") && line.Contains("/")) return;

        lock (_chatLog)
        {
            if (_chatLog.Count < 20000) _chatLog.Add(line);
        }
    }

    private Task OnSign(SignEditorEventArgs e)
    {
        _signPrompts.Enqueue(e);
        log($"sign editor opened at ({e.Position.X},{e.Position.Y},{e.Position.Z})");
        return Task.CompletedTask;
    }

    // ===== Join =====

    public async Task<bool> ConnectAndSpawnAsync(string server, int port)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            client.State.LastDisconnectTranslateKey = null;
            client.State.LastDisconnectReason = null;
            await client.ConnectAsync(server, port, false);

            var deadline = DateTime.UtcNow.AddSeconds(30);
            long lastTick = -1;
            while (DateTime.UtcNow < deadline)
            {
                if (!client.IsConnected) break;
                if (client.ProtocolState == ProtocolState.Play && client.State.LocalPlayer.HasEntity)
                {
                    var tick = client.State.Level.ClientTickCounter;
                    if (lastTick >= 0 && tick > lastTick) return true;
                    lastTick = tick;
                }
                await Task.Delay(250);
            }

            var reason = client.State.LastDisconnectTranslateKey
                         ?? client.State.LastDisconnectReason
                         ?? $"no spawn within 30s (state {client.ProtocolState})";
            log($"connect attempt {attempt}/{maxAttempts} did not reach spawn ({reason})");
            try { await client.DisconnectAsync(); } catch { /* best-effort */ }
            if (attempt < maxAttempts) await Task.Delay(4000);
        }
        return false;
    }

    public async Task SendCommandAsync(string command)
    {
        IServerboundPacket packet = new ChatCommandPacket(command);
        if (client.State.ServerSettings.EnforcesSecureChat && client.AuthResult is not null)
        {
            var signed = ChatSigning.CreateSignedChatCommandPacket(client.AuthResult, command);
            if (signed != null) packet = signed;
        }
        await client.SendPacketAsync(packet);
    }

    public Task DisconnectAsync() => client.DisconnectAsync();

    // ===== Approach =====

    public async Task<bool> WalkToAsync((int X, int Y, int Z) goal, int timeoutSec)
    {
        var baritone = baritoneProvider.CreateBaritone(client);
        log($"pathing to ({goal.X},{goal.Y},{goal.Z}), timeout {timeoutSec}s");
        baritone.GetCustomGoalProcess().SetGoalAndPath(new GoalNear(goal.X, goal.Y, goal.Z, 3));

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
        var nextReport = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && client.IsConnected)
        {
            var pos = client.State.LocalPlayer?.Entity?.Position;
            if (pos is not null)
            {
                var dist = Dist(pos, goal);
                if (dist <= 4.0)
                {
                    log($"arrived, {dist:F1} blocks from the goal");
                    baritone.GetPathingBehavior().CancelEverything();
                    await Task.Delay(1200);
                    return true;
                }
                if (DateTime.UtcNow >= nextReport)
                {
                    nextReport = DateTime.UtcNow.AddSeconds(5);
                    log($"  ...at ({pos.X:F1},{pos.Y:F1},{pos.Z:F1}), dist {dist:F1}");
                }
            }
            await Task.Delay(250);
        }

        baritone.GetPathingBehavior().CancelEverything();
        log("did not reach the goal");
        return false;
    }

    /// <summary>Finds the NPC by the text floating above it — entity ids are per-session.</summary>
    public async Task<bool> FindNpcAsync(string nameSubstring, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var self = client.State.LocalPlayer?.Entity?.Position;
            var all = client.State.WorldEntities.GetAllEntities();

            var labels = all
                .Select(e => (Entity: e, Text: LabelTextOf(e)))
                .Where(x => Matches(x.Text, nameSubstring))
                .OrderBy(x => self is null ? 0 : Dist(x.Entity.Position, self))
                .ToList();

            foreach (var (label, _) in labels)
            {
                var body = all
                    .Where(e => e.EntityId != label.EntityId
                                && LabelTextOf(e) is null
                                && Math.Abs(e.Position.X - label.Position.X) <= 1.0
                                && Math.Abs(e.Position.Z - label.Position.Z) <= 1.0
                                && label.Position.Y - e.Position.Y is >= -0.5 and <= 5.0)
                    .OrderByDescending(e => e.Position.Y)
                    .FirstOrDefault();
                if (body is not null)
                {
                    _npc = body;
                    log($"NPC \"{nameSubstring}\" is entity {body.EntityId} at ({body.Position.X:F1},{body.Position.Y:F1},{body.Position.Z:F1})");
                    return true;
                }
            }

            await Task.Delay(250);
        }
        return false;
    }

    /// <summary>Closes the last few blocks — an interact beyond the server's reach check is simply ignored.</summary>
    public async Task ApproachNpcAsync()
    {
        if (_npc is null) return;
        const double reach = 3.2;
        var self = client.State.LocalPlayer?.Entity?.Position;
        if (self is null || Dist(self, _npc.Position) <= reach) return;

        var baritone = baritoneProvider.CreateBaritone(client);
        var block = ((int)Math.Floor(_npc.Position.X), (int)Math.Floor(_npc.Position.Y), (int)Math.Floor(_npc.Position.Z));
        log($"NPC is {Dist(self, _npc.Position):F1} blocks away — closing in");
        baritone.GetCustomGoalProcess().SetGoalAndPath(new GoalNear(block.Item1, block.Item2, block.Item3, 2));

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && client.IsConnected)
        {
            var pos = client.State.LocalPlayer?.Entity?.Position;
            if (pos is not null && Dist(pos, _npc.Position) <= reach) break;
            await Task.Delay(200);
        }

        baritone.GetPathingBehavior().CancelEverything();
        await Task.Delay(1200);
    }

    /// <summary>
    /// Aims at the NPC, right-clicks it, and waits for the menu to fill in.
    ///
    /// <paramref name="expectedTitle"/> is not optional in spirit: SkyBlock hands the player a menu item in the
    /// hotbar, and Baritone right-clicks while pathing (opening gates), which pops the "Game Menu" open
    /// mid-walk. A container that is ALREADY open when we interact never fires "opened" again, so without
    /// closing first and then checking the title, the caller happily reads the wrong menu — which is exactly
    /// how a hub list that plainly contained "SkyBlock Hub #21" came back as "not in the list".
    /// </summary>
    public async Task<bool> OpenNpcMenuAsync(string? expectedTitle = null)
    {
        if (_npc is null) return false;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            if (containers.IsContainerOpen)
            {
                log($"closing a stale \"{ContainerTitle}\" before interacting");
                await containers.CloseContainerAsync();
                await Task.Delay(600);
            }

            await AimAtNpcAsync();
            await Task.Delay(400);

            var opened = await containers.InteractWithEntityAsync(
                _npc.EntityId,
                Hand.MainHand,
                new Vector3<double>(0, 1.0, 0));

            if ((opened || containers.IsContainerOpen) && await WaitForMenuContentAsync(TimeSpan.FromSeconds(6)))
            {
                if (expectedTitle is null || ContainerTitle.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                log($"opened \"{ContainerTitle}\" but wanted \"{expectedTitle}\" (attempt {attempt})");
            }
            else
            {
                log($"no menu after interact (attempt {attempt})");
            }

            await Task.Delay(1000);
        }
        return false;
    }

    /// <summary>
    /// Switches to an empty hotbar slot so a stray right-click cannot use an item. SkyBlock puts a
    /// "SkyBlock Menu (Click)" in the hotbar, and using it opens a full-screen menu that then masquerades as
    /// whatever menu the caller was expecting.
    /// </summary>
    public async Task SelectEmptyHotbarSlotAsync()
    {
        var entity = client.State.LocalPlayer?.Entity;
        if (entity is null) return;

        for (short hotbar = 0; hotbar <= 8; hotbar++)
        {
            if (!entity.Inventory.GetSlot((short)(hotbar + 36)).IsEmpty) continue;
            await client.SendPacketAsync(new SetCarriedItemPacket { Slot = hotbar });
            entity.Inventory.HeldSlot = hotbar;
            log($"holding empty hotbar slot {hotbar} so stray right-clicks do nothing");
            return;
        }

        log("no empty hotbar slot — stray right-clicks may open menus");
    }

    private async Task AimAtNpcAsync()
    {
        var entity = client.State.LocalPlayer.Entity!;
        var dx = _npc!.Position.X - entity.Position.X;
        var dy = (_npc.Position.Y + 1.0) - (entity.Position.Y + 1.62);
        var dz = _npc.Position.Z - entity.Position.Z;

        var yaw = (float)(Math.Atan2(-dx, dz) * (180.0 / Math.PI));
        var pitch = (float)(-Math.Atan2(dy, Math.Sqrt(dx * dx + dz * dz)) * (180.0 / Math.PI));

        entity.YawPitch = new Vector2<float>(yaw, pitch);
        await client.SendPacketAsync(new MovePlayerRotationPacket
        {
            Yaw = yaw,
            Pitch = pitch,
            Flags = entity.IsOnGround ? MovementFlags.OnGround : MovementFlags.None
        });
    }

    // ===== Menu interaction =====

    /// <summary>
    /// Clicks the menu slot whose display name contains <paramref name="wanted"/>, preferring the tightest
    /// match so "Buy Order" does not hit "Cancel Buy Order". Returns false if nothing matched, listing what
    /// was there — a name miss is the usual cause of a stalled chain.
    /// </summary>
    public async Task<bool> ClickAsync(string wanted, bool waitForChange = true)
    {
        var container = containers.CurrentContainer;
        if (container is null || !container.IsOpen)
        {
            log($"cannot click \"{wanted}\": no menu open");
            return false;
        }

        var containerSlots = container.Type.GetContainerSlotCount();
        var match = container.Slots
            .Where(kv => kv.Key < containerSlots && !kv.Value.IsEmpty)
            .Select(kv => (Index: kv.Key, Name: CleanName(kv.Value)))
            .Where(x => x.Name is not null && x.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name!.Length)
            .FirstOrDefault();

        if (match.Name is null)
        {
            log($"no slot named like \"{wanted}\" in \"{ContainerTitle}\"; present: " +
                string.Join(", ", container.Slots
                    .Where(kv => kv.Key < containerSlots && !kv.Value.IsEmpty && CleanName(kv.Value) is not null)
                    .OrderBy(kv => kv.Key)
                    .Select(kv => $"[{kv.Key}] {CleanName(kv.Value)}")));
            return false;
        }

        var signature = SignatureOf(container);
        log($"click [{match.Index}] \"{match.Name}\"");
        await containers.ClickSlotAsync(match.Index);

        if (!waitForChange) return true;

        await WaitForMenuChangeAsync(signature, TimeSpan.FromSeconds(6));
        return true;
    }

    /// <summary>
    /// Answers a sign-editor prompt — Hypixel's text input for Search, Custom Amount and Custom Price. The
    /// typed value goes on the first line, the prompt lines below it are echoed back untouched.
    /// </summary>
    public async Task<bool> SignAsync(string value)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        SignEditorEventArgs? prompt = null;
        while (DateTime.UtcNow < deadline && !_signPrompts.TryDequeue(out prompt))
        {
            await Task.Delay(100);
        }

        if (prompt is null)
        {
            log($"no sign editor appeared to answer with \"{value}\"");
            return false;
        }

        var existing = prompt.ExistingLines;
        string[] lines =
        [
            value,
            existing.Length > 1 ? existing[1] ?? "" : "",
            existing.Length > 2 ? existing[2] ?? "" : "",
            existing.Length > 3 ? existing[3] ?? "" : ""
        ];

        log($"sign <- \"{value}\"");
        await client.SendPacketAsync(new SignUpdatePacket
        {
            Position = prompt.Position,
            IsFrontText = prompt.IsFrontText,
            Lines = lines
        });

        return await WaitForMenuContentAsync(TimeSpan.FromSeconds(8));
    }

    /// <summary>Waits for the open menu to hold items and stop changing — contents arrive after Open Screen.</summary>
    public async Task<bool> WaitForMenuContentAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? candidate = null;
        var stableSince = DateTime.UtcNow;

        while (DateTime.UtcNow < deadline)
        {
            var current = containers.CurrentContainer;
            if (current is { IsOpen: true })
            {
                var containerSlots = current.Type.GetContainerSlotCount();
                var filled = current.Slots.Count(kv => kv.Key < containerSlots && !kv.Value.IsEmpty);
                if (filled > 0)
                {
                    var signature = SignatureOf(current);
                    if (signature != candidate)
                    {
                        candidate = signature;
                        stableSince = DateTime.UtcNow;
                    }
                    else if (DateTime.UtcNow - stableSince > TimeSpan.FromMilliseconds(600))
                    {
                        TrackOrderBook();
                        return true;
                    }
                }
            }
            await Task.Delay(100);
        }
        return false;
    }

    private async Task<bool> WaitForMenuChangeAsync(string previous, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? candidate = null;
        var stableSince = DateTime.UtcNow;

        while (DateTime.UtcNow < deadline)
        {
            var current = containers.CurrentContainer;
            var signature = current is null || !current.IsOpen ? "<closed>" : SignatureOf(current);

            if (signature != previous)
            {
                if (signature != candidate)
                {
                    candidate = signature;
                    stableSince = DateTime.UtcNow;
                }
                else if (DateTime.UtcNow - stableSince > TimeSpan.FromMilliseconds(600))
                {
                    if (signature != "<closed>") TrackOrderBook();
                    return signature != "<closed>";
                }
            }
            await Task.Delay(100);
        }
        return false;
    }

    public Task CloseAsync() => containers.IsContainerOpen ? containers.CloseContainerAsync() : Task.CompletedTask;

    private static string SignatureOf(ContainerState c) =>
        $"{c.ContainerId}|{c.Title}|" + string.Join(",", c.Slots
            .Where(kv => !kv.Value.IsEmpty)
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}:{kv.Value.ItemId}:{kv.Value.ItemCount}:{CleanName(kv.Value)}"));

    // ===== Reading the menu =====

    /// <summary>The menu's own slots (not the player inventory below it), in slot order.</summary>
    public List<MenuSlot> MenuSlots()
    {
        var container = containers.CurrentContainer;
        if (container is null) return [];

        var containerSlots = container.Type.GetContainerSlotCount();
        var result = new List<MenuSlot>();
        foreach (var (index, slot) in container.Slots.OrderBy(kv => kv.Key))
        {
            if (slot.IsEmpty || index >= containerSlots) continue;
            result.Add(new MenuSlot
            {
                Index = index,
                Row = index / 9,
                Col = index % 9,
                Region = "menu",
                Item = slot.ItemId is { } id ? items.GetItemName(id) ?? $"item:{id}" : "?",
                ItemId = slot.ItemId ?? 0,
                Count = slot.ItemCount,
                Name = CleanName(slot),
                NameRaw = ItemTextHelper.GetDisplayName(slot),
                Lore = CleanLore(slot)
            });
        }
        return result;
    }

    public MenuSlot? FindSlot(string nameSubstring) =>
        MenuSlots()
            .Where(s => s.Name is not null && s.Name.Contains(nameSubstring, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Name!.Length)
            .FirstOrDefault();

    public void LogMenu()
    {
        log($"=== \"{ContainerTitle}\" ===");
        foreach (var slot in MenuSlots())
        {
            log($"  [{slot.Index,2}] {slot.Name}");
            foreach (var line in slot.Lore.Where(l => l.Trim().Length > 0)) log($"         | {line}");
        }
    }

    /// <summary>
    /// Picks the top of book off a product page: "Create Sell Offer" lists offers best-first (the ask),
    /// "Create Buy Order" lists orders best-first (the bid).
    /// </summary>
    private void TrackOrderBook()
    {
        foreach (var slot in MenuSlots())
        {
            var top = slot.Lore.Select(l => PriceLine.Match(l)).FirstOrDefault(m => m.Success);
            if (top is null) continue;
            if (!double.TryParse(top.Groups[1].Value.Replace(",", ""),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var price)) continue;

            if (slot.Name?.Contains("Create Sell Offer", StringComparison.OrdinalIgnoreCase) == true) BestAsk = price;
            else if (slot.Name?.Contains("Create Buy Order", StringComparison.OrdinalIgnoreCase) == true) BestBid = price;
        }
    }

    // ===== Helpers =====

    public static string? CleanName(Slot slot) =>
        ItemTextHelper.GetDisplayName(slot) is { Length: > 0 } n ? ItemTextHelper.StripFormattingCodes(n) : null;

    public static List<string> CleanLore(Slot slot) =>
        ItemTextHelper.GetLore(slot).Select(ItemTextHelper.StripFormattingCodes).ToList();

    private static bool Matches(string? text, string substring) =>
        text is { Length: > 0 } && ItemTextHelper.StripFormattingCodes(text)
            .Contains(substring, StringComparison.OrdinalIgnoreCase);

    private static string TypeName(int protocolId) =>
        ClientState.EntityTypeRegistry is { } registry && registry.TryGetValue(protocolId, out var name)
            ? name
            : $"entity_type:{protocolId}";

    private static string? LabelTextOf(WorldEntity e)
    {
        if (TypeName(e.EntityType) == "minecraft:text_display")
        {
            foreach (var (_, field) in e.Metadata.OrderBy(kv => kv.Key))
            {
                if (field.TypeId != (int)SetEntityDataPacket.MetadataType.Component) continue;
                if (field.Value is NbtTag tag && NbtDump.RawText(tag) is { Length: > 0 } text) return text;
            }
            return null;
        }
        return e.CustomName is { Length: > 0 } ? e.CustomName : null;
    }

    private static double Dist(Vector3<double> a, Vector3<double> b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));

    private static double Dist(Vector3<double> a, (int X, int Y, int Z) b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
}
