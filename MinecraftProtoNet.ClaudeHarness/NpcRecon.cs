using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Event.Events;
using MinecraftProtoNet.Baritone.Api.Event.Listener;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>One command to send, and how long to wait afterwards before the next step.</summary>
public sealed record ReconStep(string Command, int WaitAfterSec);

/// <summary>
/// A scripted read-only visit to a public server: join, walk through a fixed command sequence to reach the
/// area of interest, then snapshot what the client knows about the entities around it.
///
/// Deliberately passive — it sends only the listed chat commands and never moves, places, breaks or interacts.
/// The output is a reference dump, not a test verdict.
/// </summary>
public sealed record ReconProfile(
    string Name,
    string Server,
    int Port,
    int WaitAfterJoinSec,
    IReadOnlyList<ReconStep> Steps,
    double CaptureRadius,
    string OutputSubdir,
    /// <summary>
    /// Optional block to walk to before capturing, for NPCs that are not near the spawn point. Pathing is real
    /// Baritone movement on a live public server, so it is opt-in per profile rather than the default.
    /// </summary>
    (int X, int Y, int Z)? GoalPos = null,
    int GoalTimeoutSec = 90,
    int WaitAtGoalSec = 5)
{
    public static readonly IReadOnlyDictionary<string, ReconProfile> All = new Dictionary<string, ReconProfile>
    {
        // Netherite: simpler than Hypixel and used to prove the capture pipeline first.
        ["netherite-hub"] = new(
            Name: "netherite-hub",
            Server: "hub.netherite.gg",
            Port: 25565,
            WaitAfterJoinSec: 5,
            Steps:
            [
                new ReconStep("survival", 5),
                new ReconStep("hub", 2)
            ],
            CaptureRadius: 64,
            OutputSubdir: "netherite-hub"),

        // Hypixel SkyBlock hub. /skyblock first, then /hub, because /hub alone from the main lobby returns to
        // the main lobby rather than the SkyBlock one.
        //
        // Host is mc.hypixel.net, NOT hypixel.net: the bare domain is the website (Cloudflare, port 25565
        // closed — it times out at the TCP level). Vanilla gets there anyway because it resolves the SRV record
        // _minecraft._tcp.hypixel.net -> mc.hypixel.net:25565 first; our Connection does a plain A lookup, so
        // the resolved host has to be given explicitly.
        ["hypixel-skyblock-hub"] = new(
            Name: "hypixel-skyblock-hub",
            Server: "mc.hypixel.net",
            Port: 25565,
            WaitAfterJoinSec: 5,
            Steps:
            [
                new ReconStep("skyblock", 8),
                new ReconStep("hub", 5)
            ],
            CaptureRadius: 64,
            OutputSubdir: "skyblock-hub"),

        // Same route, then WALKS to the Bazaar area of the hub (~46 blocks from spawn), which is outside the
        // server's entity tracking range from the spawn point.
        //
        // There is no warp: SkyBlock answers "/warp bazaar" with "Unknown destination! Check the Fast Travel
        // menu", so the bot has to path there. It does, once the world actually decodes — the earlier belief
        // that pathing "does not work" was the dimension-bounds bug, which made the whole world read as air.
        //
        // Radius 0 = capture everything the client knows about, so nothing is clipped by an arbitrary limit.
        ["hypixel-skyblock-bazaar"] = new(
            Name: "hypixel-skyblock-bazaar",
            Server: "mc.hypixel.net",
            Port: 25565,
            WaitAfterJoinSec: 5,
            Steps:
            [
                new ReconStep("skyblock", 8),
                new ReconStep("hub", 5)
            ],
            CaptureRadius: 0,
            OutputSubdir: "skyblock-bazaar",
            GoalPos: (-36, 72, -28),
            GoalTimeoutSec: 45),

        // The Hypixel SkyBlock hub geometry, downloaded and served by a local Paper server, so the movement
        // work can be iterated offline. Same spawn and same goal as the live profile — the point is that the
        // COLLISION divergence (falling through the bottom-slab at (-5,76,-4) that real Baritone lands on) is
        // client-side, so it reproduces here regardless of how lenient Paper's movement checks are next to
        // Hypixel's. No chat steps: there is no /skyblock or /hub on this server, the world IS the hub.
        //
        // `attribute` is sent first because the live account carries a SkyBlock speed stat (movement_speed
        // 0.1673 vs the vanilla 0.1), and per-tick step size has to match the live captures for the traces to
        // be comparable. JeffMaxxing is opped on this server.
        ["local-bazaar"] = new(
            Name: "local-bazaar",
            Server: "127.0.0.1",
            Port: 25565,
            WaitAfterJoinSec: 3,
            Steps:
            [
                // A default server spreads joins over a spawn radius, so without pinning this the bot starts
                // somewhere different every run and the traces cannot be compared against the live captures.
                // (0,77,-1) is the SkyBlock hub spawn the Hypixel runs and the Baritone captures all began at.
                new ReconStep("setworldspawn 0 77 -1", 1),
                new ReconStep("tp @s 0 77 -1", 2),
                new ReconStep("attribute @s minecraft:movement_speed base set 0.1673", 2),
                // GrimAC is installed on this server and, unlike Paper's own movement checks, it re-simulates
                // vanilla physics every tick and reports the offset between its prediction and where we said we
                // were. At the configured threshold of 0.001 that is a 1mm-resolution oracle for physics parity
                // — far better than inferring divergence from Hypixel's opaque setbacks. Both subscriptions are
                // per-player toggles delivered as system chat, so OP is enough and no server config changes.
                // (Grim does not exempt operators: grim.exempt/grim.disabled/grim.nosetback all default false.)
                //
                // Only verbose is toggled here. Alerts are already on at join (grim.alerts.enable-on-join is
                // default:op) so sending "grim alerts" would turn them OFF — and Grim drops the verbose
                // subscription along with them, silencing the very stream this is here to collect.
                new ReconStep("grim verbose", 1)
            ],
            CaptureRadius: 0,
            OutputSubdir: "local-bazaar",
            GoalPos: (-36, 72, -28),
            GoalTimeoutSec: 45),

        // Identical to local-bazaar but aimed at a SniffCraft proxy listening on 25555, which forwards to the
        // same Paper server. The point is a byte-level capture of OUR packet stream that can be diffed against
        // the Java Baritone captures over the same route. Still 127.0.0.1, so the humanizer stays inactive and
        // the run is comparable to a direct one.
        ["local-sniff"] = new(
            Name: "local-sniff",
            Server: "127.0.0.1",
            Port: 25555,
            WaitAfterJoinSec: 3,
            Steps:
            [
                new ReconStep("setworldspawn 0 77 -1", 1),
                new ReconStep("tp @s 0 77 -1", 2),
                new ReconStep("attribute @s minecraft:movement_speed base set 0.1673", 2)
            ],
            CaptureRadius: 0,
            OutputSubdir: "local-sniff",
            GoalPos: (-36, 72, -28),
            GoalTimeoutSec: 45)
    };
}

public sealed class NpcReconTask(
    IMinecraftClient client,
    IChatEventBus chatBus,
    IBaritoneProvider baritoneProvider,
    string outputRoot)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly List<string> _chatLog = [];

    private static void Log(string msg) => Console.WriteLine($"[recon] {msg}");

    /// <summary>
    /// Entity types are a built-in registry, so they never arrive over the wire; Core loads them from the
    /// generated report during configuration (<c>ClientState.InitializeEntityTypeRegistry</c>).
    /// </summary>
    private static string TypeName(int protocolId) =>
        ClientState.EntityTypeRegistry is { } registry && registry.TryGetValue(protocolId, out var name)
            ? name
            : $"entity_type:{protocolId}";

    public async Task<bool> RunAsync(ReconProfile profile)
    {
        Log($"profile={profile.Name} server={profile.Server}:{profile.Port} radius={profile.CaptureRadius}");

        if (!await client.AuthenticateAsync())
        {
            Log("AUTH FAILED — ensure an account is added/active (run the web app once to device-code login).");
            return false;
        }
        Log("authenticated");

        void OnChat(SystemChatEventArgs e)
        {
            var line = string.Join("", e.TextParts).Trim();
            if (line.Length == 0) return;

            // Anticheat verbose output (GrimAC's per-tick prediction offset) arrives as system chat, one line
            // per flagged tick. Stamping the client tick is what makes it correlatable with the movement diag;
            // without it the offsets are just an unordered pile. The cap is generous for the same reason — a
            // 45s path at 20 TPS can flag most of its ~900 ticks.
            var stamped = $"[t{client.State.Level.ClientTickCounter}] {line}";
            lock (_chatLog)
            {
                if (_chatLog.Count < 8000) _chatLog.Add(stamped);
            }
        }

        chatBus.OnSystemChat += OnChat;
        try
        {
            if (!await ConnectAndSpawnAsync(profile))
            {
                Log("CONNECT/SPAWN FAILED");
                return false;
            }
            Log("connected + spawned");

            // Create the Baritone instance as soon as we are in Play, not lazily at walk time: the game-loop
            // hook only ticks instances that exist when a tick fires, so one created later never gets driven.
            if (profile.GoalPos is not null)
            {
                baritoneProvider.CreateBaritone(client);
            }

            Log($"waiting {profile.WaitAfterJoinSec}s after join");
            await Task.Delay(TimeSpan.FromSeconds(profile.WaitAfterJoinSec));

            foreach (var step in profile.Steps)
            {
                if (!client.IsConnected)
                {
                    Log($"disconnected before '/{step.Command}' could be sent");
                    return false;
                }

                Log($"sending /{step.Command}, then waiting {step.WaitAfterSec}s");
                await SendCommandAsync(step.Command);
                await Task.Delay(TimeSpan.FromSeconds(step.WaitAfterSec));
            }

            if (!client.IsConnected)
            {
                var reason = client.State.LastDisconnectTranslateKey ?? client.State.LastDisconnectReason ?? "unknown";
                Log($"disconnected before capture ({reason})");
                return false;
            }

            if (profile.GoalPos is { } goal)
            {
                await WalkToAsync(profile, goal);
            }

            var snapshot = Capture(profile);
            var paths = Write(profile, snapshot);
            Log($"captured {snapshot.Entities.Count} entities ({snapshot.Npcs.Count} look like NPCs)");
            foreach (var p in paths) Log($"wrote {p}");
            return true;
        }
        finally
        {
            chatBus.OnSystemChat -= OnChat;
            try { await client.DisconnectAsync(); } catch { /* best-effort */ }
            Log("disconnected");
        }
    }

    // Same shape as RunController's: ConnectAsync returns before the login flow finishes, so a login-time
    // disconnect only surfaces while waiting for spawn. Retry the whole connect on failure.
    private async Task<bool> ConnectAndSpawnAsync(ReconProfile profile)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            client.State.LastDisconnectTranslateKey = null;
            client.State.LastDisconnectReason = null;
            await client.ConnectAsync(profile.Server, profile.Port, false);

            // The Play protocol state is part of the readiness check, not just an entity + a ticking loop:
            // LocalPlayer has an entity from login onwards, so a client parked in Configuration (a server
            // gating the handshake on something we have not answered) otherwise looks fully spawned.
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

    /// <summary>
    /// Walks to a block with Baritone, then idles briefly so entities around the destination finish streaming
    /// in. Falls through to capturing wherever it got to rather than failing the run — a partial approach still
    /// produces useful data, and how close it got is recorded in the snapshot.
    /// </summary>
    private async Task WalkToAsync(ReconProfile profile, (int X, int Y, int Z) goal)
    {
        var baritone = baritoneProvider.CreateBaritone(client);

        // Without this, a pathfinder that gives up instantly is indistinguishable from one that never ran.
        var pathLogger = new PathEventLogger();
        baritone.GetGameEventHandler().RegisterEventListener(pathLogger);

        ProbeGround();
        DumpGear();

        // Whether the destination chunk is even loaded decides how to read a failure: the pathfinder reads an
        // unloaded chunk as empty, so "no route" and "no world there yet" look identical without this.
        var goalChunkLoaded = client.State.Level.HasChunk(goal.X >> 4, goal.Z >> 4);
        Log($"pathing to ({goal.X},{goal.Y},{goal.Z}), timeout {profile.GoalTimeoutSec}s, goal chunk loaded={goalChunkLoaded}");

        baritone.GetCustomGoalProcess().SetGoalAndPath(new GoalNear(goal.X, goal.Y, goal.Z, 3));

        var deadline = DateTime.UtcNow.AddSeconds(profile.GoalTimeoutSec);
        var nextReport = DateTime.UtcNow.AddSeconds(3);
        var lastReport = DateTime.UtcNow;
        var lastTick = client.State.Level.ClientTickCounter;
        (double X, double Z)? lastPos = null;
        var entityInput = () => client.State.LocalPlayer.Entity!.InputState.Current;
        while (DateTime.UtcNow < deadline && client.IsConnected)
        {
            var pos = client.State.LocalPlayer?.Entity?.Position;
            if (pos is not null)
            {
                var dist = Math.Sqrt(Math.Pow(pos.X - goal.X, 2) + Math.Pow(pos.Y - goal.Y, 2) + Math.Pow(pos.Z - goal.Z, 2));
                if (dist <= 4.0)
                {
                    Log($"arrived, {dist:F1} blocks from the goal");
                    break;
                }

                if (DateTime.UtcNow >= nextReport)
                {
                    var now = DateTime.UtcNow;
                    var elapsed = (now - lastReport).TotalSeconds;
                    var tick = client.State.Level.ClientTickCounter;
                    // Client TPS and ground speed, measured rather than inferred: 20 TPS and ~4.3 blocks/s is
                    // vanilla walking, ~5.6 sprinting.
                    var tps = elapsed > 0 ? (tick - lastTick) / elapsed : 0;
                    var speed = elapsed > 0 && lastPos is not null
                        ? Math.Sqrt(Math.Pow(pos.X - lastPos.Value.X, 2) + Math.Pow(pos.Z - lastPos.Value.Z, 2)) / elapsed
                        : 0;
                    lastReport = now;
                    lastTick = tick;
                    lastPos = (pos.X, pos.Z);
                    nextReport = now.AddSeconds(3);

                    var exec = baritone.GetPathingBehavior().GetCurrent();
                    var pathInfo = exec is null
                        ? "no path"
                        : $"path {exec.GetPosition()}/{exec.GetPath().Length()}";
                    Log($"  ...at ({pos.X:F1},{pos.Y:F1},{pos.Z:F1}) dist {dist:F1}, {pathInfo}, " +
                        $"tps={tps:F1}, speed={speed:F2} b/s, sprint={entityInput().Sprint}, onGround={client.State.LocalPlayer.Entity?.IsOnGround}");
                }
            }
            await Task.Delay(250);
        }

        baritone.GetPathingBehavior().CancelEverything();

        var final = client.State.LocalPlayer?.Entity?.Position;
        if (final is not null)
        {
            var dist = Math.Sqrt(Math.Pow(final.X - goal.X, 2) + Math.Pow(final.Y - goal.Y, 2) + Math.Pow(final.Z - goal.Z, 2));
            Log($"stopped {dist:F1} blocks from the goal at ({final.X:F1},{final.Y:F1},{final.Z:F1})");
        }

        Log($"settling {profile.WaitAtGoalSec}s for entities to stream in");
        await Task.Delay(TimeSpan.FromSeconds(profile.WaitAtGoalSec));
    }

    /// <summary>
    /// Logs the worn/held gear with its SkyBlock lore, next to the movement_speed attribute the server actually
    /// sent us and the acceleration our physics derives from it.
    ///
    /// The point is to confirm that a stat the gear advertises ("Speed: +X") is genuinely reflected in what we
    /// simulate, rather than assumed. SkyBlock's Speed stat maps to vanilla movement_speed as
    /// <c>speed / 1000</c> (Speed 100 = the vanilla base 0.1), so the two numbers are directly comparable.
    /// </summary>
    private void DumpGear()
    {
        var entity = client.State.LocalPlayer?.Entity;
        if (entity is null) return;

        var speed = entity.MovementSpeed;
        Log($"gear dump — movement_speed attribute = {speed:F4} (implies SkyBlock Speed stat {speed * 1000:F0})");
        Log($"  sprint accel = {speed * 1.3:F4}/tick, walk accel = {speed:F4}/tick " +
            $"(vanilla base 0.1000 => 0.1300 sprint)");

        // Armour occupies 5..8 (helmet..boots), 45 is offhand, 36..44 the hotbar.
        foreach (var (slotIndex, label) in new (short, string)[]
                 {
                     (5, "helmet"), (6, "chestplate"), (7, "leggings"), (8, "boots"), (45, "offhand")
                 })
        {
            DumpGearSlot(entity, slotIndex, label);
        }

        for (short s = 36; s <= 44; s++)
        {
            DumpGearSlot(entity, s, $"hotbar{s - 36}");
        }
    }

    private void DumpGearSlot(MinecraftProtoNet.Core.State.Entity entity, short slotIndex, string label)
    {
        var slot = entity.Inventory.GetSlot(slotIndex);
        if (slot.IsEmpty) return;

        var itemName = slot.ItemId is { } id && ClientState.ItemRegistry.TryGetValue(id, out var registryName)
            ? registryName
            : $"item{slot.ItemId}";
        var display = ItemTextHelper.GetDisplayName(slot) is { Length: > 0 } d
            ? ItemTextHelper.StripFormattingCodes(d)
            : "(no name)";

        Log($"  {label,-10} {display}  [{itemName} x{slot.ItemCount}]");

        // Only the stat lines matter here; the rest of SkyBlock lore is flavour text.
        foreach (var line in ItemTextHelper.GetLore(slot))
        {
            var clean = ItemTextHelper.StripFormattingCodes(line).Trim();
            if (clean.Length == 0) continue;
            if (clean.Contains("Speed", StringComparison.OrdinalIgnoreCase)
                || clean.Contains("Jump", StringComparison.OrdinalIgnoreCase)
                || clean.Contains("Gravity", StringComparison.OrdinalIgnoreCase))
            {
                Log($"      STAT: {clean}");
            }
        }
    }

    /// <summary>
    /// Logs the column the player is standing in, as our world model sees it. If the bot is falling forever
    /// and the server keeps resyncing it, this says whether the ground is missing from our model (chunk/palette
    /// problem) or present but not collidable (block shape problem).
    /// </summary>
    private void ProbeGround()
    {
        var entity = client.State.LocalPlayer?.Entity;
        if (entity is null) return;

        var px = (int)Math.Floor(entity.Position.X);
        var py = (int)Math.Floor(entity.Position.Y);
        var pz = (int)Math.Floor(entity.Position.Z);

        Log($"ground probe at feet ({px},{py},{pz}), onGround={entity.IsOnGround}, chunk loaded={client.State.Level.HasChunk(px >> 4, pz >> 4)}");
        for (var y = py + 1; y >= py - 4; y--)
        {
            var state = client.State.Level.GetBlockAt(px, y, pz);
            var desc = state is null
                ? "<null - not loaded>"
                : $"{state.Name} air={state.IsAir} blocksMotion={state.BlocksMotion}";
            Log($"  y={y,-4} {desc}");
        }

        // What the physics layer actually collides with, which is the thing that decides landing.
        var box = new Core.Physics.Shapes.AABB(
            entity.Position.X - 0.3, entity.Position.Y - 0.6, entity.Position.Z - 0.3,
            entity.Position.X + 0.3, entity.Position.Y + 0.1, entity.Position.Z + 0.3);
        var aabbs = client.State.Level.GetCollidingBlockAABBs(box);
        Log($"  colliding AABBs just below the feet: {aabbs.Count}");
    }

    private async Task SendCommandAsync(string command)
    {
        IServerboundPacket packet = new ChatCommandPacket(command);
        if (client.State.ServerSettings.EnforcesSecureChat && client.AuthResult is not null)
        {
            var signed = ChatSigning.CreateSignedChatCommandPacket(client.AuthResult, command);
            if (signed != null) packet = signed;
        }

        if (client is MinecraftClient mc)
            await mc.SendPacketAsync(packet);
        else
            throw new InvalidOperationException("IMinecraftClient is not a MinecraftClient; cannot send raw packet.");
    }

    // ===== Capture =====

    private ReconSnapshot Capture(ReconProfile profile)
    {
        var self = client.State.LocalPlayer.Entity;
        var selfPos = self?.Position;

        var all = client.State.WorldEntities.GetAllEntities();
        var players = client.State.Level.GetAllPlayers();
        var playersByEntityId = players
            .Where(p => p.HasEntity)
            .GroupBy(p => p.Entity!.EntityId)
            .ToDictionary(g => g.Key, g => g.First());
        var playersByUuid = players
            .GroupBy(p => p.Uuid)
            .ToDictionary(g => g.Key, g => g.First());

        // An NPC's visible label is a separate entity floating above it — historically a named armour stand,
        // but modern servers use text_display. Both are collected here and matched to their owner below rather
        // than reported as unexplained nameless entries.
        var labelHolders = all
            .Select(e => (Entity: e, Text: LabelTextOf(e)))
            .Where(x => x.Text is { Length: > 0 })
            .ToList();
        var labelHolderIds = labelHolders.Select(x => x.Entity.EntityId).ToHashSet();

        var entities = new List<ReconEntity>();
        foreach (var e in all)
        {
            var typeName = TypeName(e.EntityType);
            var distance = selfPos is null ? 0 : Distance(e.Position, selfPos);
            if (profile.CaptureRadius > 0 && selfPos is not null && distance > profile.CaptureRadius) continue;

            playersByEntityId.TryGetValue(e.EntityId, out var linked);
            linked ??= playersByUuid.GetValueOrDefault(e.Uuid);

            // Labels sit directly above their owner. Ordered top-down so multi-line labels read in the order
            // they appear in game. A label holder is not given labels of its own.
            var labels = labelHolderIds.Contains(e.EntityId)
                ? []
                : labelHolders
                    .Where(h => Math.Abs(h.Entity.Position.X - e.Position.X) <= 1.0
                                && Math.Abs(h.Entity.Position.Z - e.Position.Z) <= 1.0
                                // Tall enough to reach the top line of a multi-line label: four-line labels on
                                // a 2-block NPC put the title ~4.2 blocks up, so a 4.0 window silently clipped
                                // exactly the line that names the NPC.
                                && h.Entity.Position.Y - e.Position.Y is >= -0.5 and <= 5.0)
                    .OrderByDescending(h => h.Entity.Position.Y)
                    .Select(h => h.Text!)
                    .ToList();

            var entityProfile = ProfileOf(e);

            entities.Add(new ReconEntity
            {
                EntityId = e.EntityId,
                Uuid = e.Uuid.ToString(),
                TypeId = e.EntityType,
                Type = typeName,
                X = Math.Round(e.Position.X, 3),
                Y = Math.Round(e.Position.Y, 3),
                Z = Math.Round(e.Position.Z, 3),
                Yaw = Math.Round(e.YawPitch.X, 2),
                Pitch = Math.Round(e.YawPitch.Y, 2),
                Distance = Math.Round(distance, 2),
                CustomName = e.CustomName,
                CustomNameRaw = NbtDump.RawText(e.CustomNameComponent) is { Length: > 0 } raw ? raw : null,
                CustomNameComponent = NbtDump.ToPlain(e.CustomNameComponent),
                CustomNameVisible = e.CustomNameVisible,
                Invisible = e.IsInvisible,
                Classification = Classify(typeName, e, linked, labelHolderIds.Contains(e.EntityId)),
                LabelText = LabelTextOf(e),
                ProfileName = entityProfile?.Name,
                ProfileUuid = entityProfile?.Uuid?.ToString(),
                LinkedPlayer = linked is null ? null : new ReconPlayer
                {
                    Uuid = linked.Uuid.ToString(),
                    Username = linked.Username,
                    DisplayNameRaw = NbtDump.RawText(linked.DisplayName) is { Length: > 0 } dn ? dn : null,
                    DisplayNameClean = linked.DisplayName is null ? null : ItemTextHelper.StripFormattingCodes(NbtDump.RawText(linked.DisplayName)),
                    Listed = linked.IsListed,
                    HasSkin = linked.Properties.Any(p => p.Name == "textures")
                },
                NameTags = labels.Count == 0 ? null : labels,
                Metadata = e.Metadata
                    .OrderBy(kv => kv.Key)
                    .ToDictionary(kv => kv.Key.ToString(), kv => DescribeMetadata(kv.Value))
            });
        }

        entities = entities.OrderBy(e => e.Distance).ToList();

        // Labels are excluded: they describe an NPC, they are not one. Unlisted players are excluded too —
        // too weak on its own to call something an NPC.
        var npcs = entities
            .Where(e => e.Classification is "npc-fake-player" or "npc-mannequin" or "named-entity")
            .ToList();

        List<string> chat;
        lock (_chatLog) chat = [.. _chatLog];

        return new ReconSnapshot
        {
            Profile = profile.Name,
            Server = $"{profile.Server}:{profile.Port}",
            CapturedUtc = DateTime.UtcNow,
            ProtocolVersion = ProtocolConstants.ProtocolVersion,
            CaptureRadius = profile.CaptureRadius,
            Self = self is null
                ? null
                : new ReconSelf
                {
                    X = Math.Round(self.Position.X, 3),
                    Y = Math.Round(self.Position.Y, 3),
                    Z = Math.Round(self.Position.Z, 3),
                    Yaw = Math.Round(self.YawPitch.X, 2),
                    Pitch = Math.Round(self.YawPitch.Y, 2)
                },
            TabListCount = players.Count,
            Entities = entities,
            Npcs = npcs,
            ChatLog = chat
        };
    }

    /// <summary>
    /// Labels an entity by what it looks like from the wire, not by any server-specific knowledge.
    ///
    /// The player cases are a hint, not a verdict: a player entity with no tab-list entry at all is how hub
    /// servers spawn human-looking NPCs, but an unlisted entry is weaker evidence — some servers simply hide
    /// real players from the tab list — so the two are reported separately instead of both being called NPCs.
    /// </summary>
    private static string Classify(string typeName, WorldEntity e, Player? linked, bool isLabelHolder)
    {
        if (typeName == "minecraft:player")
        {
            if (linked is null) return "npc-fake-player";
            return linked.IsListed ? "player" : "player-unlisted";
        }

        // Purpose-built NPC entity added in 26.x: a posed player model driven by a resolvable profile.
        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/entity/decoration/Mannequin.java
        if (typeName == "minecraft:mannequin") return "npc-mannequin";

        if (isLabelHolder) return "label";

        return e.CustomName is { Length: > 0 } ? "named-entity" : typeName.Replace("minecraft:", "");
    }

    /// <summary>
    /// The visible text an entity contributes as a floating label, or null if it is not a label.
    /// Covers both the armour-stand-with-a-custom-name convention and text_display entities, whose text lives
    /// in a Component data field (DATA_TEXT_ID, index 23 for a plain text_display).
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/entity/Display.java:770
    /// </summary>
    private static string? LabelTextOf(WorldEntity e)
    {
        var typeName = TypeName(e.EntityType);

        if (typeName == "minecraft:text_display")
        {
            // Found by declared serializer rather than a hard-coded index: the index is only stable for a
            // given entity class and protocol, the Component type is not ambiguous here.
            foreach (var (_, field) in e.Metadata.OrderBy(kv => kv.Key))
            {
                if (field.TypeId != (int)SetEntityDataPacket.MetadataType.Component) continue;
                if (field.Value is NbtTag tag && NbtDump.RawText(tag) is { Length: > 0 } text) return text;
            }
            return null;
        }

        return e.CustomName is { Length: > 0 } ? e.CustomName : null;
    }

    /// <summary>
    /// The game profile an entity carries, if any — the identity and skin behind a mannequin NPC.
    /// </summary>
    private static ResolvableProfileData? ProfileOf(WorldEntity e) =>
        e.Metadata.Values
            .Select(f => f.Value)
            .OfType<ResolvableProfileData>()
            .FirstOrDefault();

    /// <summary>
    /// Renders one synched-data field, keeping the serializer the server declared for it. The index alone does
    /// not identify a field — the same index means different things on different entity classes — so the type
    /// is what makes a raw dump interpretable later.
    /// </summary>
    private static object DescribeMetadata(EntityDataValue field) => new ReconMetadataField
    {
        Type = field.TypeId >= 0 && Enum.IsDefined(typeof(SetEntityDataPacket.MetadataType), field.TypeId)
            ? ((SetEntityDataPacket.MetadataType)field.TypeId).ToString()
            : $"unknown({field.TypeId})",
        Value = DescribeValue(field.Value)
    };

    /// <summary>
    /// Types come straight from SetEntityDataPacket's deserialiser, so this covers the shapes it can produce
    /// and falls back to ToString() rather than dropping unknown fields.
    /// </summary>
    private static object? DescribeValue(object? value) => value switch
    {
        null => null,
        NbtTag tag => NbtDump.ToPlain(tag),
        Slot slot => new { item = slot.ItemId, count = slot.ItemCount },
        ResolvableProfileData profile => new
        {
            profile.Name,
            uuid = profile.Uuid?.ToString(),
            profile.IsResolved,
            properties = profile.Properties.Select(p => new { p.Name, valueLength = p.Value.Length }).ToList(),
            // The base64 textures blob is long and is the actual skin identity, so keep it in full.
            textures = profile.TexturesValue,
            skin = profile.Skin
        },
        string or bool or byte or short or int or long or float or double => value,
        Enum e => e.ToString(),
        _ => value.ToString()
    };

    private static double Distance(Core.Models.Core.Vector3<double> a, Core.Models.Core.Vector3<double> b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));

    // ===== Output =====

    private List<string> Write(ReconProfile profile, ReconSnapshot snapshot)
    {
        var dir = Path.Combine(outputRoot, profile.OutputSubdir);
        Directory.CreateDirectory(dir);

        var stamp = snapshot.CapturedUtc.ToString("yyyyMMdd-HHmmss");
        var written = new List<string>();

        var json = JsonSerializer.Serialize(snapshot, Json);
        foreach (var name in new[] { $"npcs-{stamp}.json", "npcs-latest.json" })
        {
            var path = Path.Combine(dir, name);
            File.WriteAllText(path, json);
            written.Add(path);
        }

        var mdPath = Path.Combine(dir, "npcs-latest.md");
        File.WriteAllText(mdPath, RenderMarkdown(snapshot));
        written.Add(mdPath);

        return written;
    }

    private static string RenderMarkdown(ReconSnapshot s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# NPC capture — {s.Profile}");
        sb.AppendLine();
        sb.AppendLine($"- Server: `{s.Server}`");
        sb.AppendLine($"- Captured: {s.CapturedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- Client protocol: {s.ProtocolVersion}");
        if (s.Self is not null) sb.AppendLine($"- Bot position: {s.Self.X:F1}, {s.Self.Y:F1}, {s.Self.Z:F1}");
        sb.AppendLine($"- Capture radius: {s.CaptureRadius} blocks");
        sb.AppendLine($"- Entities in range: {s.Entities.Count} ({s.Npcs.Count} classified as NPC-like)");
        sb.AppendLine();

        sb.AppendLine("## NPC-like entities");
        sb.AppendLine();
        if (s.Npcs.Count == 0)
        {
            sb.AppendLine("_None found._");
        }
        else
        {
            sb.AppendLine("| Name | Type | Class | Position | Dist | Label lines |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var n in s.Npcs)
            {
                var name = n.CustomName
                           ?? n.ProfileName
                           ?? n.LinkedPlayer?.DisplayNameClean
                           ?? n.LinkedPlayer?.Username
                           ?? (n.NameTags is { Count: > 0 } ? Strip(n.NameTags[0]) : "(unnamed)");
                var labels = n.NameTags is { Count: > 0 }
                    ? string.Join(" ⏎ ", n.NameTags.Select(Strip))
                    : "";
                sb.AppendLine($"| {Escape(name)} | `{n.Type}` | {n.Classification} | {n.X:F1}, {n.Y:F1}, {n.Z:F1} | {n.Distance:F1} | {Escape(labels)} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## All entities in range");
        sb.AppendLine();
        sb.AppendLine("| Id | Type | Name | Position | Dist | Flags |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var e in s.Entities)
        {
            var flags = new List<string>();
            if (e.Invisible) flags.Add("invisible");
            if (e.CustomNameVisible) flags.Add("name-visible");
            if (e.NameTags is { Count: > 0 }) flags.Add($"{e.NameTags.Count} nametag(s)");
            var name = e.CustomName
                       ?? e.ProfileName
                       ?? (e.LabelText is { } label ? Strip(label) : null)
                       ?? e.LinkedPlayer?.Username
                       ?? "";
            sb.AppendLine($"| {e.EntityId} | `{e.Type}` | {Escape(name)} | {e.X:F1}, {e.Y:F1}, {e.Z:F1} | {e.Distance:F1} | {string.Join(", ", flags)} |");
        }

        if (s.ChatLog.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Chat during the visit");
            sb.AppendLine();
            sb.AppendLine("```");
            foreach (var line in s.ChatLog) sb.AppendLine(line);
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    private static string Escape(string text) => text.Replace("|", "\\|").Replace("\n", " ");

    // The JSON keeps § codes; the human-readable table drops them so names stay legible.
    private static string Strip(string text) => ItemTextHelper.StripFormattingCodes(text);
}

// ===== Serialised shapes =====

public sealed class ReconSnapshot
{
    public required string Profile { get; init; }
    public required string Server { get; init; }
    public required DateTime CapturedUtc { get; init; }
    public required int ProtocolVersion { get; init; }
    public required double CaptureRadius { get; init; }
    public ReconSelf? Self { get; init; }
    public required int TabListCount { get; init; }
    public required List<ReconEntity> Entities { get; init; }
    public required List<ReconEntity> Npcs { get; init; }
    public required List<string> ChatLog { get; init; }
}

public sealed class ReconSelf
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Z { get; init; }
    public required double Yaw { get; init; }
    public required double Pitch { get; init; }
}

public sealed class ReconEntity
{
    public required int EntityId { get; init; }
    public required string Uuid { get; init; }
    public required int TypeId { get; init; }
    public required string Type { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Z { get; init; }
    public required double Yaw { get; init; }
    public required double Pitch { get; init; }
    public required double Distance { get; init; }
    public string? CustomName { get; init; }
    public string? CustomNameRaw { get; init; }
    public object? CustomNameComponent { get; init; }
    public required bool CustomNameVisible { get; init; }
    public required bool Invisible { get; init; }
    public required string Classification { get; init; }

    /// <summary>Set when this entity IS a floating label (text_display / named armour stand).</summary>
    public string? LabelText { get; init; }

    /// <summary>Profile name behind a mannequin NPC or player head, when one is carried.</summary>
    public string? ProfileName { get; init; }

    public string? ProfileUuid { get; init; }

    public ReconPlayer? LinkedPlayer { get; init; }

    /// <summary>Labels floating above this entity, top line first.</summary>
    public List<string>? NameTags { get; init; }

    public required Dictionary<string, object> Metadata { get; init; }
}

/// <summary>
/// Logs Baritone path events during a recon walk. A goal that produces no movement can mean the pathfinder
/// never ran or that it calculated and gave up immediately; only the events distinguish the two.
/// </summary>
public sealed class PathEventLogger : IGameEventListener
{
    public void OnPathEvent(PathEvent evt) => Console.WriteLine($"[recon]   PATH {evt}");

    public void OnTick(TickEvent evt) { }
    public void OnPostTick(TickEvent evt) { }
    public void OnPlayerUpdate(PlayerUpdateEvent evt) { }
    public void OnSendChatMessage(ChatEvent evt) { }
    public void OnPreTabComplete(TabCompleteEvent evt) { }
    public void OnChunkEvent(ChunkEvent evt) { }
    public void OnBlockChange(BlockChangeEvent evt) { }
    public void OnRenderPass(RenderEvent evt) { }
    public void OnWorldEvent(WorldEvent evt) { }
    public void OnSendPacket(PacketEvent evt) { }
    public void OnReceivePacket(PacketEvent evt) { }
    public void OnPlayerRotationMove(RotationMoveEvent evt) { }
    public void OnPlayerSprintState(SprintStateEvent evt) { }
    public void OnBlockInteract(BlockInteractEvent evt) { }
    public void OnPlayerDeath() { }
}

/// <summary>One synched-data field as written to the reference dump.</summary>
public sealed class ReconMetadataField
{
    public required string Type { get; init; }
    public object? Value { get; init; }
}

public sealed class ReconPlayer
{
    public required string Uuid { get; init; }
    public string? Username { get; init; }
    public string? DisplayNameRaw { get; init; }
    public string? DisplayNameClean { get; init; }
    public required bool Listed { get; init; }
    public required bool HasSkin { get; init; }
}
