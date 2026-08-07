using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// What a menu-recon run should visit and click.
///
/// Deliberately data-driven rather than a fixed script: the point of these runs is to LEARN a server's menu
/// layout, so the same code has to work for the Hub Selector (one click, one outcome) and for the Bazaar
/// (a tree of menus that takes several passes to map).
/// </summary>
public sealed record MenuProfile(
    string Name,
    string Server,
    int Port,
    IReadOnlyList<ReconStep> Steps,
    /// <summary>Substring of the NPC's floating label (or custom name) to interact with.</summary>
    string NpcName,
    (int X, int Y, int Z)? GoalPos,
    int GoalTimeoutSec,
    string OutputSubdir)
{
    public static readonly IReadOnlyDictionary<string, MenuProfile> All = new Dictionary<string, MenuProfile>
    {
        // Hub Selector NPC, ~24 blocks from the SkyBlock hub spawn point. Found by the hypixel-skyblock-hub
        // recon profile at (-5.5, 69.0, -22.5); resolved by label at runtime because the entity id is
        // per-session.
        ["hub-selector"] = new(
            Name: "hub-selector",
            Server: "mc.hypixel.net",
            Port: 25565,
            Steps: [new ReconStep("skyblock", 8), new ReconStep("hub", 5)],
            NpcName: "Hub Selector",
            GoalPos: (-5, 69, -22),
            GoalTimeoutSec: 60,
            OutputSubdir: "hub-selector"),

        // Bazaar NPCs stand next to the movement-course goal block at (-36,72,-28). Recon only: the click
        // chain is supplied per run so nothing that trades is ever clicked by default.
        ["bazaar"] = new(
            Name: "bazaar",
            Server: "mc.hypixel.net",
            Port: 25565,
            Steps: [new ReconStep("skyblock", 8), new ReconStep("hub", 5)],
            NpcName: "Bazaar",
            GoalPos: (-36, 72, -28),
            GoalTimeoutSec: 90,
            OutputSubdir: "bazaar")
    };
}

/// <summary>
/// Joins a public server, walks to a named NPC, right-clicks it, and dumps every menu that results — including
/// the menus reached by a supplied chain of slot clicks.
///
/// This is a RECON tool. It never picks a slot on its own: each click has to be named on the command line, so a
/// run can only touch the buttons it was told to. Slots whose name or lore looks like it commits a trade are
/// refused outright even when named, so an exploratory chain cannot accidentally buy or sell.
/// </summary>
/// <summary>One step of a menu walk: click a slot by name, or answer a sign-editor prompt with text.</summary>
public sealed record MenuStep(MenuStepKind Kind, string Value);

public enum MenuStepKind
{
    Click,
    Sign
}

public sealed class MenuReconTask(
    IMinecraftClient client,
    IChatEventBus chatBus,
    ISignEventBus signBus,
    IBaritoneProvider baritoneProvider,
    IContainerManager containers,
    IItemRegistryService items,
    string outputRoot,
    bool allowTrade = false)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Names/lore that mean the click COMMITS something rather than navigating. Refused even if asked for, so
    /// that mapping the Bazaar tree cannot spend or sell anything by accident.
    /// </summary>
    private static readonly string[] CommitKeywords =
    [
        "buy instantly", "sell instantly", "sell inventory", "sell sacks", "confirm", "instant buy",
        "instant sell", "flip", "claim", "sell now", "buy now",
        // The Bazaar's price-selection screen is the last step before an order exists — every button on it
        // reads "Click to proceed!", and proceeding is what places it. Recon stops at that screen.
        "same as top order", "top order +", "of spread", "custom price",
        // Cancelling someone's live order is destructive even though it buys nothing.
        "cancel"
    ];

    private readonly List<string> _chatLog = [];
    private readonly List<MenuDump> _dumps = [];
    private readonly System.Collections.Concurrent.ConcurrentQueue<SignEditorEventArgs> _signPrompts = new();

    /// <summary>
    /// Best ask (lowest sell offer) and best bid (highest buy order) as of the last product page seen. Kept so
    /// a chain can price an order to cross the spread — `--sign "{ask}"` — instead of carrying a number over
    /// from an earlier run, which is stale the moment anyone else trades.
    /// </summary>
    private double? _bestAsk;
    private double? _bestBid;

    private static readonly System.Text.RegularExpressions.Regex PriceLine =
        new(@"-\s*([\d,]+(?:\.\d+)?)\s*coins each", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static void Log(string msg) => Console.WriteLine($"[menu] {msg}");

    public async Task<bool> RunAsync(MenuProfile profile, IReadOnlyList<MenuStep> steps)
    {
        Log($"profile={profile.Name} server={profile.Server}:{profile.Port} npc=\"{profile.NpcName}\"");
        if (steps.Count > 0)
            Log($"steps: {string.Join(" -> ", steps.Select(s => $"{s.Kind.ToString().ToLowerInvariant()}(\"{s.Value}\")"))}");
        if (allowTrade)
            Log("!! --allow-trade: the commit guard is OFF for this run, buttons that spend coins WILL be clicked");

        if (!await client.AuthenticateAsync())
        {
            Log("AUTH FAILED — ensure an account is added/active (run the web app once to device-code login).");
            return false;
        }

        void OnChat(SystemChatEventArgs e)
        {
            var line = string.Join("", e.TextParts).Trim();
            if (line.Length == 0) return;
            lock (_chatLog)
            {
                if (_chatLog.Count < 4000) _chatLog.Add($"[t{client.State.Level.ClientTickCounter}] {line}");
            }
        }

        // Registered for the whole run, not around a single step: the sign editor opens as a consequence of the
        // click before it, so the prompt can land before the step that answers it is reached.
        Task OnSign(SignEditorEventArgs e)
        {
            _signPrompts.Enqueue(e);
            Log($"sign editor opened at ({e.Position.X},{e.Position.Y},{e.Position.Z}), " +
                $"existing lines: [{string.Join(" | ", e.ExistingLines.Select(l => l ?? ""))}]");
            return Task.CompletedTask;
        }

        chatBus.OnSystemChat += OnChat;
        signBus.OnSignEditorOpened += OnSign;
        try
        {
            if (!await ConnectAndSpawnAsync(profile)) { Log("CONNECT/SPAWN FAILED"); return false; }
            Log("connected + spawned");

            // Created here, not at walk time: the game-loop hook only ticks instances that already exist when a
            // tick fires, so one created later never gets driven.
            baritoneProvider.CreateBaritone(client);

            await Task.Delay(TimeSpan.FromSeconds(5));

            foreach (var step in profile.Steps)
            {
                if (!client.IsConnected) { Log($"disconnected before '/{step.Command}'"); return false; }
                Log($"sending /{step.Command}, waiting {step.WaitAfterSec}s");
                await SendCommandAsync(step.Command);
                await Task.Delay(TimeSpan.FromSeconds(step.WaitAfterSec));
            }

            if (profile.GoalPos is { } goal && !await WalkToAsync(profile, goal)) return false;

            var npc = await FindNpcAsync(profile.NpcName, TimeSpan.FromSeconds(15));
            if (npc is null)
            {
                Log($"NPC matching \"{profile.NpcName}\" not found near the destination");
                DumpNearbyLabels();
                return false;
            }

            Log($"NPC id={npc.EntityId} type={TypeName(npc.EntityType)} at ({npc.Position.X:F2},{npc.Position.Y:F2},{npc.Position.Z:F2})");

            // The profile's goal is a place to walk to, not necessarily arm's reach of the NPC — and an
            // interact beyond the server's reach check is simply ignored. Close the last few blocks once the
            // NPC's real position is known.
            await ApproachNpcAsync(npc);

            if (!await OpenNpcMenuAsync(npc)) { Log("no menu opened after interacting"); return false; }

            // The open event fires on the OpenScreen packet, which carries only the title — the items arrive
            // afterwards in Set Container Content. Capturing on the event alone records an empty menu.
            await WaitForMenuContentAsync(TimeSpan.FromSeconds(6));
            Capture("root", $"opened by right-clicking \"{profile.NpcName}\"");
            PrintLastDump();

            foreach (var step in steps)
            {
                var ok = step.Kind switch
                {
                    MenuStepKind.Click => await ClickByNameAsync(step.Value),
                    MenuStepKind.Sign => await AnswerSignAsync(step.Value),
                    _ => false
                };
                if (!ok) return false;
            }

            // Whatever the last click did — switch server, print an error, open another menu — shows up in the
            // seconds after it, so give it time before the run tears the connection down.
            Log("settling 8s to catch the outcome (chat / world change)");
            var before = client.State.LocalPlayer?.Entity?.Position;
            await Task.Delay(TimeSpan.FromSeconds(8));
            var after = client.State.LocalPlayer?.Entity?.Position;
            if (before is not null && after is not null)
            {
                Log($"position before/after: ({before.X:F1},{before.Y:F1},{before.Z:F1}) -> ({after.X:F1},{after.Y:F1},{after.Z:F1})");
            }

            foreach (var path in Write(profile)) Log($"wrote {path}");
            PrintChatTail(25);
            return true;
        }
        finally
        {
            chatBus.OnSystemChat -= OnChat;
            signBus.OnSignEditorOpened -= OnSign;
            try { await client.DisconnectAsync(); } catch { /* best-effort */ }
            Log("disconnected");
        }
    }

    // ===== Join =====

    private async Task<bool> ConnectAndSpawnAsync(MenuProfile profile)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            client.State.LastDisconnectTranslateKey = null;
            client.State.LastDisconnectReason = null;
            await client.ConnectAsync(profile.Server, profile.Port, false);

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
            Log($"connect attempt {attempt}/{maxAttempts} did not reach spawn ({reason})");
            try { await client.DisconnectAsync(); } catch { /* best-effort */ }
            if (attempt < maxAttempts) await Task.Delay(4000);
        }
        return false;
    }

    private async Task SendCommandAsync(string command)
    {
        IServerboundPacket packet = new ChatCommandPacket(command);
        if (client.State.ServerSettings.EnforcesSecureChat && client.AuthResult is not null)
        {
            var signed = ChatSigning.CreateSignedChatCommandPacket(client.AuthResult, command);
            if (signed != null) packet = signed;
        }
        await client.SendPacketAsync(packet);
    }

    // ===== Approach =====

    private async Task<bool> WalkToAsync(MenuProfile profile, (int X, int Y, int Z) goal)
    {
        var baritone = baritoneProvider.CreateBaritone(client);
        baritone.GetGameEventHandler().RegisterEventListener(new PathEventLogger());

        Log($"pathing to ({goal.X},{goal.Y},{goal.Z}), timeout {profile.GoalTimeoutSec}s");
        baritone.GetCustomGoalProcess().SetGoalAndPath(new GoalNear(goal.X, goal.Y, goal.Z, 3));

        var deadline = DateTime.UtcNow.AddSeconds(profile.GoalTimeoutSec);
        var nextReport = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && client.IsConnected)
        {
            var pos = client.State.LocalPlayer?.Entity?.Position;
            if (pos is not null)
            {
                var dist = Dist(pos, goal);
                if (dist <= 4.0)
                {
                    Log($"arrived, {dist:F1} blocks from the goal");
                    baritone.GetPathingBehavior().CancelEverything();
                    // Baritone releases the movement inputs on cancel, but the player still carries the
                    // velocity it had; interacting mid-slide is what makes an NPC click land on nothing.
                    await Task.Delay(1200);
                    return true;
                }
                if (DateTime.UtcNow >= nextReport)
                {
                    nextReport = DateTime.UtcNow.AddSeconds(3);
                    Log($"  ...at ({pos.X:F1},{pos.Y:F1},{pos.Z:F1}), dist {dist:F1}");
                }
            }
            await Task.Delay(250);
        }

        baritone.GetPathingBehavior().CancelEverything();
        var final = client.State.LocalPlayer?.Entity?.Position;
        Log(final is null
            ? "did not reach the goal (no position)"
            : $"did not reach the goal, stopped {Dist(final, goal):F1} blocks away");
        return false;
    }

    /// <summary>
    /// Walks the last stretch to the NPC if the profile goal left us outside interaction range. Vanilla's
    /// reach is 3 blocks from the eyes, and servers enforce it, so anything past ~3.5 has to be closed first.
    /// </summary>
    private async Task ApproachNpcAsync(WorldEntity npc)
    {
        const double reach = 3.2;
        var self = client.State.LocalPlayer?.Entity?.Position;
        if (self is null || Dist(self, npc.Position) <= reach) return;

        var baritone = baritoneProvider.CreateBaritone(client);
        var block = ((int)Math.Floor(npc.Position.X), (int)Math.Floor(npc.Position.Y), (int)Math.Floor(npc.Position.Z));
        Log($"NPC is {Dist(self, npc.Position):F1} blocks away — closing in on ({block.Item1},{block.Item2},{block.Item3})");
        baritone.GetCustomGoalProcess().SetGoalAndPath(new GoalNear(block.Item1, block.Item2, block.Item3, 2));

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && client.IsConnected)
        {
            var pos = client.State.LocalPlayer?.Entity?.Position;
            if (pos is not null && Dist(pos, npc.Position) <= reach) break;
            await Task.Delay(200);
        }

        baritone.GetPathingBehavior().CancelEverything();
        await Task.Delay(1200);

        var final = client.State.LocalPlayer?.Entity?.Position;
        if (final is not null) Log($"now {Dist(final, npc.Position):F1} blocks from the NPC");
    }

    /// <summary>
    /// Resolves the NPC by the text floating above it rather than by entity id, which is per-session, or by
    /// position, which moves when Hypixel reshuffles a hub.
    ///
    /// Hypixel NPCs are fake players with their name on a separate label entity roughly 2 blocks above; the
    /// interactable body is the nearest non-label entity under that label.
    /// </summary>
    private async Task<WorldEntity?> FindNpcAsync(string nameSubstring, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var self = client.State.LocalPlayer?.Entity?.Position;
            var all = client.State.WorldEntities.GetAllEntities();

            // Direct hit: the entity carries the name itself.
            var direct = all
                .Where(e => Matches(e.CustomName, nameSubstring))
                .OrderBy(e => self is null ? 0 : Dist(e.Position, self))
                .FirstOrDefault();
            if (direct is not null && TypeName(direct.EntityType) is not "minecraft:text_display" and not "minecraft:armor_stand")
                return direct;

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
                if (body is not null) return body;
            }

            await Task.Delay(250);
        }
        return null;
    }

    /// <summary>Lists what IS around, so a name miss can be corrected without another blind run.</summary>
    private void DumpNearbyLabels()
    {
        var self = client.State.LocalPlayer?.Entity?.Position;
        var named = client.State.WorldEntities.GetAllEntities()
            .Select(e => (e, Text: LabelTextOf(e) ?? e.CustomName))
            .Where(x => x.Text is { Length: > 0 })
            .OrderBy(x => self is null ? 0 : Dist(x.e.Position, self))
            .Take(30);
        Log("nearby labelled entities:");
        foreach (var (e, text) in named)
        {
            Log($"  {ItemTextHelper.StripFormattingCodes(text!),-40} ({e.Position.X:F1},{e.Position.Y:F1},{e.Position.Z:F1}) dist={(self is null ? 0 : Dist(e.Position, self)):F1}");
        }
    }

    /// <summary>
    /// Aims at the NPC and right-clicks it, vanilla-style: InteractAt (with the hit offset) followed by
    /// Interact, which is the pair the real client sends and what server-side reach checks expect.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/multiplayer/MultiPlayerGameMode.java
    /// </summary>
    private async Task<bool> OpenNpcMenuAsync(WorldEntity npc)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await AimAtAsync(npc);
            await Task.Delay(400);

            var entity = client.State.LocalPlayer.Entity!;
            var eye = new Vector3<double>(entity.Position.X, entity.Position.Y + 1.62, entity.Position.Z);
            var target = new Vector3<double>(npc.Position.X, npc.Position.Y + 1.0, npc.Position.Z);

            Log($"interacting (attempt {attempt}), eye->npc distance {Dist(eye, target):F2}");

            // One packet, not the old InteractAt+Interact pair: 26.x carries the hit location in the single
            // Interact packet. Location is relative to the NPC's own position.
            if (await containers.InteractWithEntityAsync(
                    npc.EntityId,
                    Hand.MainHand,
                    new Vector3<double>(0, target.Y - npc.Position.Y, 0)))
            {
                return true;
            }

            // A container that was already open when the interact landed never fires "opened" again.
            if (containers.IsContainerOpen) return true;
            Log("no menu yet; retrying");
            await Task.Delay(1000);
        }
        return false;
    }

    private async Task AimAtAsync(WorldEntity npc)
    {
        var entity = client.State.LocalPlayer.Entity!;
        var dx = npc.Position.X - entity.Position.X;
        var dy = (npc.Position.Y + 1.0) - (entity.Position.Y + 1.62);
        var dz = npc.Position.Z - entity.Position.Z;

        var yaw = (float)(Math.Atan2(-dx, dz) * (180.0 / Math.PI));
        var pitch = (float)(-Math.Atan2(dy, Math.Sqrt(dx * dx + dz * dz)) * (180.0 / Math.PI));

        entity.YawPitch = new Vector2<float>(yaw, pitch);
        await client.SendPacketAsync(new MovePlayerRotationPacket
        {
            Yaw = yaw,
            Pitch = pitch,
            Flags = entity.IsOnGround ? MovementFlags.OnGround : MovementFlags.None
        });
        Log($"aimed at the NPC: yaw={yaw:F1} pitch={pitch:F1}");
    }

    // ===== Clicking =====

    private async Task<bool> ClickByNameAsync(string wanted)
    {
        var container = containers.CurrentContainer;
        if (container is null || !container.IsOpen)
        {
            Log($"cannot click \"{wanted}\": no menu open");
            return false;
        }

        var containerSlots = container.Type.GetContainerSlotCount();
        var match = container.Slots
            .Where(kv => kv.Key < containerSlots && !kv.Value.IsEmpty)
            .Select(kv => (Index: kv.Key, Name: CleanName(kv.Value), Slot: kv.Value))
            .Where(x => x.Name is not null && x.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name!.Length) // prefer the tightest match, not just the lowest slot
            .FirstOrDefault();

        if (match.Name is null)
        {
            Log($"no slot named like \"{wanted}\" in \"{CleanTitle(container)}\". Slots present:");
            foreach (var kv in container.Slots.Where(kv => kv.Key < containerSlots && !kv.Value.IsEmpty).OrderBy(kv => kv.Key))
            {
                Log($"  [{kv.Key,2}] {CleanName(kv.Value)}");
            }
            return false;
        }

        // Refuse anything that commits a trade even when it was asked for by name — recon runs map the tree,
        // they do not spend.
        var haystack = (match.Name + " " + string.Join(" ", CleanLore(match.Slot))).ToLowerInvariant();
        var commit = CommitKeywords.FirstOrDefault(k => haystack.Contains(k));
        if (commit is not null)
        {
            if (!allowTrade)
            {
                Log($"REFUSING slot [{match.Index}] \"{match.Name}\": looks like it commits ({commit}). Recon runs do not trade.");
                return false;
            }
            Log($"!! slot [{match.Index}] \"{match.Name}\" commits ({commit}) — clicking it because --allow-trade was passed");
        }

        var signature = SignatureOf(container);
        Log($"clicking slot [{match.Index}] \"{match.Name}\" (matched \"{wanted}\")");
        await containers.ClickSlotAsync(match.Index);

        var changed = await WaitForMenuChangeAsync(signature, TimeSpan.FromSeconds(6));
        if (!changed)
        {
            Log("menu did not change within 6s (the click may have closed it or done nothing visible)");
            Capture($"after-click:{match.Name}", "no visible menu change");
            return true;
        }

        Capture($"after-click:{match.Name}", $"result of clicking slot {match.Index}");
        PrintLastDump();
        return true;
    }

    /// <summary>
    /// Waits for the open menu to become something else. A Hypixel click can either open a NEW window or
    /// rewrite the CURRENT one in place, so this watches the rendered contents rather than the open event.
    /// Also settles: a menu arrives slot-by-slot, and dumping mid-fill records a half-built layout.
    /// </summary>
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
                    return signature != "<closed>";
                }
            }
            await Task.Delay(100);
        }
        return false;
    }

    /// <summary>
    /// Answers a sign-editor prompt — Hypixel's text input for "Search", "Custom Amount" and "Custom Price".
    /// The server closes the menu, sends Open Sign Editor at a throwaway block, and waits for a Sign Update;
    /// the typed value goes on the first line and the prompt lines below it are echoed back untouched, which
    /// is exactly what the real client sends.
    /// </summary>
    private async Task<bool> AnswerSignAsync(string rawValue)
    {
        var value = ResolveSignValue(rawValue);
        var deadline = DateTime.UtcNow.AddSeconds(8);
        SignEditorEventArgs? prompt = null;
        while (DateTime.UtcNow < deadline && !_signPrompts.TryDequeue(out prompt))
        {
            await Task.Delay(100);
        }

        if (prompt is null)
        {
            Log($"no sign editor appeared to answer with \"{value}\"");
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

        Log($"answering the sign with \"{value}\"");
        await client.SendPacketAsync(new SignUpdatePacket
        {
            Position = prompt.Position,
            IsFrontText = prompt.IsFrontText,
            Lines = lines
        });

        // The answer makes the server re-open the menu it interrupted, carrying the value through.
        if (!await WaitForMenuContentAsync(TimeSpan.FromSeconds(8)))
        {
            Log("no menu came back after the sign answer");
            return false;
        }

        Capture($"after-sign:{value}", $"menu returned after answering the sign with \"{value}\"");
        PrintLastDump();
        return true;
    }

    /// <summary>
    /// Waits until the open menu actually has items in it and has stopped changing. Hypixel fills a menu over
    /// several packets — and sometimes rewrites it once more a beat later — so both conditions matter.
    /// </summary>
    private async Task<bool> WaitForMenuContentAsync(TimeSpan timeout)
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
                        Log($"menu settled with {filled} filled slots");
                        return true;
                    }
                }
            }
            await Task.Delay(100);
        }

        Log("menu never filled in within the timeout");
        return false;
    }

    private static string SignatureOf(ContainerState c) =>
        $"{c.ContainerId}|{c.Title}|" + string.Join(",", c.Slots
            .Where(kv => !kv.Value.IsEmpty)
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}:{kv.Value.ItemId}:{kv.Value.ItemCount}:{CleanName(kv.Value)}"));

    // ===== Dumping =====

    private void Capture(string label, string note)
    {
        var container = containers.CurrentContainer;
        if (container is null)
        {
            Log($"capture '{label}': no container");
            return;
        }

        var containerSlots = container.Type.GetContainerSlotCount();
        var slots = new List<MenuSlot>();
        foreach (var (index, slot) in container.Slots.OrderBy(kv => kv.Key))
        {
            if (slot.IsEmpty) continue;
            slots.Add(new MenuSlot
            {
                Index = index,
                Row = index < containerSlots ? index / 9 : null,
                Col = index < containerSlots ? index % 9 : null,
                Region = index < containerSlots ? "menu" : "player-inventory",
                Item = slot.ItemId is { } id ? items.GetItemName(id) ?? $"item:{id}" : "?",
                ItemId = slot.ItemId ?? 0,
                Count = slot.ItemCount,
                Name = CleanName(slot),
                NameRaw = ItemTextHelper.GetDisplayName(slot),
                Lore = CleanLore(slot),
                Components = DescribeComponents(slot)
            });
        }

        List<string> chat;
        lock (_chatLog) chat = [.. _chatLog];

        var dump = new MenuDump
        {
            Label = label,
            Note = note,
            CapturedUtc = DateTime.UtcNow,
            ContainerId = container.ContainerId,
            MenuType = container.Type.ToString(),
            MenuSlotCount = containerSlots,
            TitleRaw = container.Title,
            Title = ItemTextHelper.StripFormattingCodes(container.Title),
            Slots = slots,
            ChatAtCapture = chat.Count > 0 ? chat[^Math.Min(10, chat.Count)..] : []
        };
        _dumps.Add(dump);
        TrackOrderBook(slots);
    }

    /// <summary>
    /// Picks the top of book off a product page. "Create Buy Order" lists the standing BUY orders best-first
    /// (the bid), "Create Sell Offer" the standing SELL offers best-first (the ask) — both as
    /// "- 11,715.8 coins each | 396x in 1 order".
    /// </summary>
    private void TrackOrderBook(List<MenuSlot> slots)
    {
        foreach (var slot in slots)
        {
            var top = slot.Lore.Select(l => PriceLine.Match(l)).FirstOrDefault(m => m.Success);
            if (top is null) continue;
            if (!double.TryParse(top.Groups[1].Value.Replace(",", ""),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var price)) continue;

            if (slot.Name?.Contains("Create Sell Offer", StringComparison.OrdinalIgnoreCase) == true)
            {
                _bestAsk = price;
                Log($"top of book: ask (lowest sell offer) = {price}");
            }
            else if (slot.Name?.Contains("Create Buy Order", StringComparison.OrdinalIgnoreCase) == true)
            {
                _bestBid = price;
                Log($"top of book: bid (highest buy order) = {price}");
            }
        }
    }

    /// <summary>
    /// Substitutes {ask}/{bid} in a sign answer with the live top of book. Pricing a buy order AT the ask (or
    /// a sell offer at the bid) is a limit order that crosses, so it fills immediately — which is the point:
    /// it exercises the order path rather than the instant-buy path, without leaving an order hanging.
    /// </summary>
    private string ResolveSignValue(string value)
    {
        var resolved = value;
        foreach (var (token, price) in new[] { ("{ask}", _bestAsk), ("{bid}", _bestBid) })
        {
            if (!resolved.Contains(token, StringComparison.OrdinalIgnoreCase)) continue;
            if (price is null)
            {
                Log($"cannot resolve {token}: no product page with an order book has been seen yet");
                continue;
            }

            var text = price.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            resolved = resolved.Replace(token, text, StringComparison.OrdinalIgnoreCase);
            Log($"resolved {token} -> {text}");
        }
        return resolved;
    }

    private void PrintLastDump()
    {
        if (_dumps.Count == 0) return;
        var d = _dumps[^1];
        Log($"=== MENU \"{d.Title}\" ({d.MenuType}, {d.MenuSlotCount} menu slots, id={d.ContainerId}) [{d.Label}] ===");
        foreach (var s in d.Slots.Where(s => s.Region == "menu"))
        {
            Log($"  [{s.Index,2}] r{s.Row}c{s.Col} {s.Count,2}x {s.Item,-32} {s.Name}");
            foreach (var line in s.Lore.Where(l => l.Trim().Length > 0)) Log($"          | {line}");
        }
    }

    private void PrintChatTail(int count)
    {
        List<string> chat;
        lock (_chatLog) chat = [.. _chatLog];
        if (chat.Count == 0) return;
        Log($"--- last {Math.Min(count, chat.Count)} chat lines ---");
        foreach (var line in chat[^Math.Min(count, chat.Count)..])
        {
            Log($"  {ItemTextHelper.StripFormattingCodes(line)}");
        }
    }

    private static string? CleanName(Slot slot) =>
        ItemTextHelper.GetDisplayName(slot) is { Length: > 0 } n ? ItemTextHelper.StripFormattingCodes(n) : null;

    private static List<string> CleanLore(Slot slot) =>
        ItemTextHelper.GetLore(slot).Select(ItemTextHelper.StripFormattingCodes).ToList();

    private static string CleanTitle(ContainerState c) => ItemTextHelper.StripFormattingCodes(c.Title);

    /// <summary>
    /// Every component the slot carries other than the name/lore already rendered above — this is where the
    /// server hides the data a bot needs (custom model ids, skull profiles, hidden tooltips).
    /// </summary>
    private static Dictionary<string, object?>? DescribeComponents(Slot slot)
    {
        if (slot.ComponentsToAdd is null || slot.ComponentsToAdd.Length == 0) return null;
        var result = new Dictionary<string, object?>();
        foreach (var component in slot.ComponentsToAdd)
        {
            if (component.Type is ComponentType.CustomName or ComponentType.ItemName or ComponentType.Lore) continue;
            result[component.Type.ToString()] = component.Data switch
            {
                NbtTag tag => NbtDump.ToPlain(tag),
                object?[] array => array.Select(x => x is NbtTag t ? NbtDump.ToPlain(t) : x?.ToString()).ToList(),
                null => null,
                var other => other.ToString()
            };
        }
        return result.Count == 0 ? null : result;
    }

    private List<string> Write(MenuProfile profile)
    {
        var dir = Path.Combine(outputRoot, "menus", profile.OutputSubdir);
        Directory.CreateDirectory(dir);

        List<string> chat;
        lock (_chatLog) chat = [.. _chatLog];

        var snapshot = new MenuSnapshot
        {
            Profile = profile.Name,
            Server = $"{profile.Server}:{profile.Port}",
            CapturedUtc = DateTime.UtcNow,
            ProtocolVersion = ProtocolConstants.ProtocolVersion,
            Npc = profile.NpcName,
            Menus = _dumps,
            ChatLog = chat
        };

        var stamp = snapshot.CapturedUtc.ToString("yyyyMMdd-HHmmss");
        var written = new List<string>();
        var json = JsonSerializer.Serialize(snapshot, Json);
        foreach (var name in new[] { $"menu-{stamp}.json", "menu-latest.json" })
        {
            var path = Path.Combine(dir, name);
            File.WriteAllText(path, json);
            written.Add(path);
        }

        var mdPath = Path.Combine(dir, "menu-latest.md");
        File.WriteAllText(mdPath, RenderMarkdown(snapshot));
        written.Add(mdPath);
        return written;
    }

    private static string RenderMarkdown(MenuSnapshot s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Menu capture — {s.Profile}");
        sb.AppendLine();
        sb.AppendLine($"- Server: `{s.Server}`  NPC: `{s.Npc}`");
        sb.AppendLine($"- Captured: {s.CapturedUtc:yyyy-MM-dd HH:mm:ss} UTC (protocol {s.ProtocolVersion})");
        sb.AppendLine();

        foreach (var menu in s.Menus)
        {
            sb.AppendLine($"## {menu.Label} — \"{menu.Title}\"");
            sb.AppendLine();
            sb.AppendLine($"- `{menu.MenuType}`, {menu.MenuSlotCount} menu slots, window id {menu.ContainerId}");
            sb.AppendLine($"- {menu.Note}");
            sb.AppendLine();
            sb.AppendLine("| Slot | r,c | Item | Count | Name | Lore |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var slot in menu.Slots.Where(x => x.Region == "menu"))
            {
                var lore = string.Join(" ⏎ ", slot.Lore.Where(l => l.Trim().Length > 0));
                sb.AppendLine($"| {slot.Index} | {slot.Row},{slot.Col} | `{slot.Item}` | {slot.Count} | {Escape(slot.Name ?? "")} | {Escape(lore)} |");
            }
            sb.AppendLine();
        }

        if (s.ChatLog.Count > 0)
        {
            sb.AppendLine("## Chat during the visit");
            sb.AppendLine();
            sb.AppendLine("```");
            foreach (var line in s.ChatLog) sb.AppendLine(ItemTextHelper.StripFormattingCodes(line));
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    private static string Escape(string text) => text.Replace("|", "\\|").Replace("\n", " ");

    // ===== Shared helpers =====

    private static bool Matches(string? text, string substring) =>
        text is { Length: > 0 } && ItemTextHelper.StripFormattingCodes(text)
            .Contains(substring, StringComparison.OrdinalIgnoreCase);

    private static string TypeName(int protocolId) =>
        ClientState.EntityTypeRegistry is { } registry && registry.TryGetValue(protocolId, out var name)
            ? name
            : $"entity_type:{protocolId}";

    /// <summary>Same rule as the NPC recon: text_display carries a Component field, older labels a custom name.</summary>
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

// ===== Serialised shapes =====

public sealed class MenuSnapshot
{
    public required string Profile { get; init; }
    public required string Server { get; init; }
    public required DateTime CapturedUtc { get; init; }
    public required int ProtocolVersion { get; init; }
    public required string Npc { get; init; }
    public required List<MenuDump> Menus { get; init; }
    public required List<string> ChatLog { get; init; }
}

public sealed class MenuDump
{
    public required string Label { get; init; }
    public required string Note { get; init; }
    public required DateTime CapturedUtc { get; init; }
    public required int ContainerId { get; init; }
    public required string MenuType { get; init; }
    public required int MenuSlotCount { get; init; }
    public required string TitleRaw { get; init; }
    public required string Title { get; init; }
    public required List<MenuSlot> Slots { get; init; }
    public required List<string> ChatAtCapture { get; init; }
}

public sealed class MenuSlot
{
    public required short Index { get; init; }
    public int? Row { get; init; }
    public int? Col { get; init; }
    public required string Region { get; init; }
    public required string Item { get; init; }
    public required int ItemId { get; init; }
    public required int Count { get; init; }
    public string? Name { get; init; }
    public string? NameRaw { get; init; }
    public required List<string> Lore { get; init; }
    public Dictionary<string, object?>? Components { get; init; }
}
