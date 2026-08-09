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
using MinecraftProtoNet.Bazaar.Services;
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

    /// <summary>
    /// Title/subtitle/action-bar text, kept separately from chat. It never lands in a chat log, so if staff
    /// address the bot this way it would otherwise vanish — and it is the first thing worth reading after an
    /// intercept.
    /// </summary>
    private readonly List<string> _screenText = [];
    private readonly System.Collections.Concurrent.ConcurrentQueue<SignEditorEventArgs> _signPrompts = new();
    private WorldEntity? _npc;
    private string _npcName = "";
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

    private CancellationTokenSource? _watchdog;

    public void Subscribe()
    {
        if (_subscribed) return;
        chatBus.OnSystemChat += OnChat;
        chatBus.OnScreenText += OnScreenText;
        signBus.OnSignEditorOpened += OnSign;
        _watchdog = new CancellationTokenSource();
        _ = WatchForUnexpectedTeleportsAsync(_watchdog.Token);
        _ = KeepAwakeAsync(_watchdog.Token);
        _subscribed = true;
    }

    public void Unsubscribe()
    {
        if (!_subscribed) return;
        chatBus.OnSystemChat -= OnChat;
        chatBus.OnScreenText -= OnScreenText;
        signBus.OnSignEditorOpened -= OnSign;
        _watchdog?.Cancel();
        _watchdog?.Dispose();
        _watchdog = null;
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

        NoteIfOutage(line);
        NoteIfRestart(line);
        NoteIfEjected(line);
        NoteIfIntercepted(line);
    }

    /// <summary>
    /// True once something happened that a human has to look at. The caller stops trading and disconnects; the
    /// latch on disk is what stops the next run from reconnecting.
    /// </summary>
    public bool Intercepted { get; private set; }

    /// <summary>
    /// When the current "a teleport is expected" window ends. A DEADLINE rather than a flag, because these
    /// windows overlap: /hub arms one, and the hub-selector click that follows arms another before the first
    /// has expired. With a boolean, the earlier window's timer switched the flag off a second after the later
    /// one switched it on, and the hub switch was reported as an unexplained teleport. Windows may only ever
    /// be extended.
    /// </summary>
    private DateTime _relocationExpectedUntil = DateTime.MinValue;

    public bool ExpectRelocation
    {
        get => DateTime.UtcNow < _relocationExpectedUntil;
        set
        {
            if (value) ExpectRelocationFor(TimeSpan.FromSeconds(25));
            else _relocationExpectedUntil = DateTime.MinValue;
        }
    }

    /// <summary>Extends the expected-teleport window; never shortens it.</summary>
    public void ExpectRelocationFor(TimeSpan window)
    {
        var until = DateTime.UtcNow + window;
        if (until > _relocationExpectedUntil) _relocationExpectedUntil = until;
    }

    private static readonly string[] InterceptPhrases =
    [
        // Being frozen for a check, and the usual staff vocabulary around it.
        "frozen", "freeze", "do not log out", "don't log out", "under investigation", "being investigated",
        "staff member", "an admin", "[admin]", "watchdog has", "you have been banned", "you are banned",
        "temporarily banned", "punishment", "appeal"
    ];

    /// <summary>
    /// Lines that contain the alarming words but are broadcast to the entire server every few minutes.
    ///
    /// Hypixel announces its ban statistics to everyone ("Watchdog has banned 7,745 players in the last 7
    /// days"), which trips the staff-language tripwire on a message that has nothing to do with us. Halting on
    /// it is worse than useless: the latch is deliberately one-way and needs a human to clear, so a routine
    /// broadcast ends an unattended session and leaves live orders unmanaged.
    /// </summary>
    private static readonly string[] GlobalBroadcasts =
    [
        "in the last 7 days", "in the last day", "staff have banned", "watchdog has banned",
        "total bans", "players in the last"
    ];

    private void NoteIfIntercepted(string line)
    {
        if (Intercepted) return;

        var lower = line.ToLowerInvariant();

        // Checked first: a broadcast that merely mentions bans is not an intercept, however it is worded.
        if (GlobalBroadcasts.Any(b => lower.Contains(b))) return;

        var phrase = InterceptPhrases.FirstOrDefault(p => lower.Contains(p));
        if (phrase is null) return;

        RaiseIntercept("staff/ban language in chat", $"matched \"{phrase}\" in: {line}");
    }

    /// <summary>
    /// Trips the latch: writes the notice, stops the bot moving, and marks the session so the caller bails out.
    /// Deliberately one-way — nothing in the bot clears it.
    /// </summary>
    public void RaiseIntercept(string reason, string details)
    {
        if (Intercepted) return;
        Intercepted = true;

        var pos = client.State.LocalPlayer?.Entity?.Position;
        var position = pos is null ? null : $"({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})";

        log($"!!!! INTERCEPT: {reason} — {details}");
        log("!!!! disconnecting and halting; a human must acknowledge before the bot runs again");

        StopMoving();

        // Disconnect here rather than letting the caller unwind to it. The previous version logged this line
        // and then kept walking for 36 seconds while the trading flow finished what it was doing — which, had
        // the intercept been a genuine staff freeze, is exactly the behaviour the tripwire exists to prevent.
        _ = Task.Run(async () =>
        {
            try { await client.DisconnectAsync(); }
            catch { /* the halt file is written either way */ }
        });

        // Both streams go into the notice: what was said in chat, and what was painted on the screen.
        var context = new List<string>();
        var screen = ScreenTextSnapshot();
        if (screen.Count > 0)
        {
            context.Add("--- on-screen text (titles / subtitles / action bar) ---");
            context.AddRange(screen.TakeLast(25));
            context.Add("");
        }
        context.Add("--- chat ---");
        context.AddRange(ChatSnapshot().TakeLast(40));

        InterceptGuard.Raise(reason, details, position, context);
    }

    /// <summary>
    /// Watches for teleports nobody asked for. Hypixel moves players around legitimately (hub switches,
    /// evacuations, our own /hub), so those windows are flagged by <see cref="ExpectRelocation"/>; anything
    /// else that throws the bot 20+ blocks in a second is someone else's hand.
    /// </summary>
    private async Task WatchForUnexpectedTeleportsAsync(CancellationToken ct)
    {
        Vector3<double>? last = null;
        var lastEntityId = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (Intercepted) return;
            if (!client.IsConnected) { last = null; continue; }

            var entity = client.State.LocalPlayer?.Entity;
            var pos = entity?.Position;
            if (pos is null || entity is null) { last = null; continue; }

            // A join or a server transfer brings a fresh Login, and with it a new entity id. Every relocation
            // that comes with one is the server moving us between worlds — joining, switching hub, being
            // evacuated — none of which is an admin picking us up. The case worth catching is a teleport
            // WITHIN a world, where the entity id is unchanged, so a changed id resets the baseline instead of
            // raising the alarm. Three false positives (a hub switch, an expiring window, and the join
            // teleport itself) all came from not making that distinction.
            if (entity.EntityId != lastEntityId)
            {
                lastEntityId = entity.EntityId;
                last = null;
                continue;
            }

            if (last is not null && !ExpectRelocation && !Evacuated && RestartWarningAt is null)
            {
                var jump = Dist(pos, last);
                // Sprinting tops out near 7 blocks/s, so 20 in a second cannot be movement we produced.
                if (jump > 20)
                {
                    RaiseIntercept("unexplained teleport",
                        $"moved {jump:F1} blocks in ~1s to ({pos.X:F1},{pos.Y:F1},{pos.Z:F1}) with no hub switch, " +
                        "/hub or evacuation in flight");
                    return;
                }
            }

            last = pos;
        }
    }

    /// <summary>
    /// Set by "This server will restart soon" / "You have 60 seconds to warp out!". A hub that is about to
    /// reboot will evacuate everyone to their island, so this is 60 seconds of warning to be somewhere else.
    /// </summary>
    public DateTime? RestartWarningAt { get; private set; }

    /// <summary>
    /// Set by "Evacuating to Your Island...". The island is dangerous ground for a bot: it spawns in the air
    /// over a void and falling is fatal, so nothing may move while this is set.
    /// </summary>
    public bool Evacuated { get; private set; }

    public void ClearRestartState()
    {
        RestartWarningAt = null;
        Evacuated = false;
    }

    /// <summary>
    /// Set when Hypixel drops us off the SkyBlock backend and into the lobby ("A kick occurred in your
    /// connection..."), which it does silently as far as the game state is concerned.
    ///
    /// This is the failure that ends sessions without announcing itself. The lobby is a DIFFERENT world, but
    /// the entity ids we cached still resolve against stale data, so the bot keeps sending interacts at an NPC
    /// that is not there, gets no menu, and retries forever while holding live orders. Nothing else in the
    /// session detects it: there is no disconnect, no world-change packet we act on, and the position jump
    /// looks like an ordinary teleport.
    /// </summary>
    public string? LobbyEjection { get; private set; }

    public void ClearLobbyEjection() => LobbyEjection = null;

    /// <summary>Whether the underlying client still has a live connection.</summary>
    public bool IsConnected => client.IsConnected;

    /// <summary>
    /// Sustained ceiling on menu actions (container clicks, sign submits, NPC right-clicks, closes), in
    /// actions per minute. Override with MCPROTO_MENU_RATE.
    ///
    /// Why a ceiling and not just per-click delays: the delays are per call site and say nothing about the
    /// rate over a minute, so a burst of menu work stays under every individual delay and still runs far
    /// hotter than a person. Across 20 ejections the menu packets are skewed toward the kick (mean relative
    /// position 0.707, 16/20 above 0.5, sign test p=0.012), while a control account that never opens a
    /// container has not been ejected at all — and packet rate and bytes are both BELOW a real client's, so
    /// the global limiter cannot be what is firing.
    /// </summary>
    private static readonly double MenuActionsPerMinute =
        double.TryParse(Environment.GetEnvironmentVariable("MCPROTO_MENU_RATE"), out var r) && r > 0 ? r : 12.0;

    private readonly Queue<DateTime> _menuActions = new();

    /// <summary>
    /// Minimum gap between two NPC menu OPENS, in milliseconds. Override with MCPROTO_NPC_OPEN_GAP_MS.
    ///
    /// The per-minute ceiling above bounds the average and says nothing about bursts, which is what the kicks
    /// actually follow: reading the order book and then repricing re-runs the whole chain (right-click the NPC,
    /// open the Bazaar, open Manage Orders, close) twice within about four seconds, and the disconnect lands
    /// ~2s after it — median lag from the last menu packet to the kick is 2s across 21 ejections. Spacing the
    /// OPENS apart is the narrowest change that removes the burst without slowing the work inside a menu.
    /// </summary>
    private static readonly int NpcOpenGapMs =
        int.TryParse(Environment.GetEnvironmentVariable("MCPROTO_NPC_OPEN_GAP_MS"), out var g) && g >= 0 ? g : 6000;

    private DateTime _lastNpcOpen = DateTime.MinValue;

    /// <summary>
    /// Blocks until another menu action would sit inside the sustained budget, using a rolling one-minute
    /// window. Bursts are still allowed — a person clicking through a menu does burst — but the average
    /// cannot exceed the ceiling.
    /// </summary>
    private async Task MenuGateAsync(string what, CancellationToken ct = default)
    {
        while (true)
        {
            DateTime? waitUntil = null;
            lock (_menuActions)
            {
                var now = DateTime.UtcNow;
                while (_menuActions.Count > 0 && now - _menuActions.Peek() > TimeSpan.FromMinutes(1))
                    _menuActions.Dequeue();

                if (_menuActions.Count < MenuActionsPerMinute)
                {
                    _menuActions.Enqueue(now);
                    return;
                }

                waitUntil = _menuActions.Peek() + TimeSpan.FromMinutes(1);
            }

            var delay = waitUntil.Value - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                log($"  menu rate gate: holding {what} for {delay.TotalSeconds:F1}s " +
                    $"(ceiling {MenuActionsPerMinute:F0}/min)");
                await Task.Delay(delay, ct);
            }
        }
    }

    private void NoteIfEjected(string line)
    {
        if (LobbyEjection is not null) return;

        var lower = line.ToLowerInvariant();
        var ejected = lower.Contains("kick occurred in your connection")
                      || (lower.Contains("you were put in the") && lower.Contains("lobby"))
                      || lower.Contains("sending packets too fast");

        if (!ejected) return;

        LobbyEjection = line;
        log($"!! EJECTED TO LOBBY: {line}");

        // Captured here and not later: the rate history is a rolling window, so the seconds that caused the
        // kick are gone within two minutes of it happening.
        var traffic = client.DumpRecentOutbound() + "\n\nfinal packets, both directions:\n" + client.DumpRecentPackets();
        log($"!! outbound traffic before the kick — {traffic}");
        WriteEjectionReport(line, traffic);

        // The cached NPC belongs to the world we just left. Keeping it is what turns one kick into a session
        // that never recovers.
        _npc = null;
        StopMoving();
    }

    /// <summary>
    /// Records a kick to a file, appended so that repeated kicks can be compared against each other — the
    /// pattern across occurrences is what identifies the cause, not any single one.
    /// </summary>
    private void WriteEjectionReport(string line, string traffic)
    {
        try
        {
            // Pinned by environment when set, because two accounts now run at once and a path derived from the
            // binary's own location sends a build that lives outside the usual bin/ tree to a different file.
            // Both arms must land in ONE file: the comparison between them is the entire point.
            var root = Environment.GetEnvironmentVariable("MCPROTO_REPORT_ROOT")
                       ?? new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Parent?.Parent?.FullName
                       ?? AppContext.BaseDirectory;

            var path = Path.Combine(root, "_ServerReferences", "lobby-ejections.md");

            var pos = client.State.LocalPlayer?.Entity?.Position;
            var report =
                $"\n## {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                $"- account: `{client.AuthResult?.Username ?? "unknown"}`" +
                $" ({Environment.GetEnvironmentVariable("MCPROTO_ARM") ?? "trading"} arm)\n" +
                $"- message: `{line}`\n" +
                $"- position: {(pos is null ? "unknown" : $"({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})")}\n" +
                $"- entity id: {client.State.LocalPlayer?.Entity?.EntityId}\n\n" +
                $"```\n{traffic}\n```\n";

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, report);
            log($"!! kick report appended to {path}");
        }
        catch (Exception ex)
        {
            log($"!! could not write the kick report ({ex.Message})");
        }
    }

    private void NoteIfRestart(string line)
    {
        // Same reasoning as NoteIfOutage: "restart soon" is also a thing players say to each other, and acting
        // on it evacuates a hub that was never going to restart.
        if (LooksLikePlayerChat(line)) return;

        if (line.Contains("restart soon", StringComparison.OrdinalIgnoreCase)
            || line.Contains("warp out", StringComparison.OrdinalIgnoreCase))
        {
            if (RestartWarningAt is null)
            {
                RestartWarningAt = DateTime.UtcNow;
                log($"!! SERVER RESTART WARNING: {line}");
            }
            return;
        }

        if (line.Contains("Evacuating to Your Island", StringComparison.OrdinalIgnoreCase))
        {
            Evacuated = true;
            log($"!! EVACUATED TO ISLAND: {line} — freezing all movement");
            StopMoving();
        }
    }

    /// <summary>
    /// Drops every movement input and cancels pathing. Used the moment an evacuation is seen: the bot lands on
    /// its island somewhere it did not choose, and a pathfinder that starts walking there can walk it off the
    /// edge.
    /// </summary>
    /// <summary>
    /// Waits, the way a person waits — with the occasional jump or few steps rather than perfect stillness.
    ///
    /// The bot spends most of its life doing nothing: orders take minutes to fill and the loop polls once a
    /// minute. A character standing at exactly one coordinate, facing exactly one direction, for six hours is
    /// the single most obvious thing about it to anyone stood nearby, and none of the packet-level care
    /// elsewhere in this class disguises it.
    ///
    /// Movement is leashed to where the wait began. Wandering off is not a theoretical risk here — a stale
    /// pathing goal walked the bot 50 blocks from the Bazaar earlier and stranded a session — so each burst
    /// is a fraction of a second and anything past a few blocks walks back rather than further out.
    /// </summary>
    /// <summary>
    /// Stand PERFECTLY still while idling (MCPROTO_NO_FIDGET=1) — no fidget, no anchor walk, nothing.
    ///
    /// Purely a measurement mode. The vanilla control captures are a human standing still, so comparing them
    /// against our normal idle (which fidgets every 12-35s by design) compares two different activities: it
    /// inflates our Move Player Pos rate and manufactures position corrections that the stationary reference
    /// could never produce. Anything derived from an "idle" diff is meaningless unless both sides are actually
    /// idle.
    /// </summary>
    private static readonly bool FidgetDisabled =
        Environment.GetEnvironmentVariable("MCPROTO_NO_FIDGET") == "1";

    public async Task IdleAsync(TimeSpan duration, CancellationToken ct = default)
    {
        if (FidgetDisabled)
        {
            StopMoving();
            var stillUntil = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < stillUntil && client.IsConnected && !Intercepted && LobbyEjection is null)
            {
                await Task.Delay(250, ct);
            }
            return;
        }

        var deadline = DateTime.UtcNow + duration;
        var anchor = client.State.LocalPlayer?.Entity?.Position;

        while (DateTime.UtcNow < deadline && client.IsConnected && !Intercepted && LobbyEjection is null)
        {
            // Most of the wait is spent still; fidgeting constantly would be as unnatural as never moving.
            var quiet = TimeSpan.FromSeconds(Random.Shared.Next(12, 35));
            var until = DateTime.UtcNow + quiet;
            if (until > deadline) until = deadline;

            while (DateTime.UtcNow < until && client.IsConnected && !Intercepted)
            {
                await Task.Delay(250, ct);
            }

            if (DateTime.UtcNow >= deadline || !client.IsConnected || Intercepted || LobbyEjection is not null) break;
            if (containers.IsContainerOpen) continue; // never fidget with a menu open

            await FidgetAsync(anchor, ct);
        }

        StopMoving();
    }

    /// <summary>The scoreboard sidebar as text, top to bottom. Empty until the server sends one.</summary>
    public List<string> SidebarLines() =>
        client.State.Level.Sidebar.Lines(client.State.Level.Teams);

    /// <summary>
    /// Waits for the sidebar to arrive and returns its lines, so a caller can tell where it already is.
    ///
    /// Worth waiting for rather than assuming: every unnecessary warp is a backend transfer, and the bot
    /// reconnects often enough for those to add up.
    /// </summary>
    public async Task<List<string>> WaitForSidebarAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && client.IsConnected)
        {
            var lines = SidebarLines();
            if (lines.Count > 0) return lines;
            await Task.Delay(250);
        }
        return SidebarLines();
    }

    /// <summary>When the bot last actually moved. Hypixel's idle timer counts input, not position packets.</summary>
    private DateTime _lastMovementUtc = DateTime.UtcNow;

    /// <summary>
    /// How long the bot may go without moving before it is nudged.
    ///
    /// Hypixel warns at about five minutes and then moves the player to the lobby, which is what was ending
    /// these sessions. Ninety seconds leaves a wide margin, and the cost of an unnecessary nudge is a few
    /// steps that look like a bored player — the same thing a bored player actually does.
    /// </summary>
    private static readonly TimeSpan AfkNudgeAfter = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Moves a little, wherever the bot happens to be. Unlike <see cref="IdleAsync"/> this is not tied to the
    /// polling wait, so it also covers the long stretches spent working menus or standing at an NPC — which
    /// is where the idle timer was quietly running out.
    /// </summary>
    public async Task NudgeAsync(CancellationToken ct = default)
    {
        if (!client.IsConnected || Intercepted) return;

        var anchor = client.State.LocalPlayer?.Entity?.Position;
        await FidgetAsync(anchor, ct);
        _lastMovementUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Watches the idle timer for the life of the session and nudges before Hypixel loses patience.
    ///
    /// A heartbeat rather than something the trading loop has to remember: the loop's shape changes, and any
    /// path through it that forgets to move costs the whole session.
    /// </summary>
    private async Task KeepAwakeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);

                if (!client.IsConnected || Intercepted || LobbyEjection is not null) continue;
                if (DateTime.UtcNow - _lastMovementUtc < AfkNudgeAfter) continue;

                // Never while a menu is open: vanilla cannot walk with a screen up, so doing it here would be
                // a more obvious tell than the idling it is meant to disguise.
                if (containers.IsContainerOpen) continue;

                log("anti-AFK nudge");
                await NudgeAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // A failed nudge is not worth ending the session over; the next tick tries again.
            }
        }
    }

    /// <summary>One short burst of movement: a jump, a few steps, or a look around.</summary>
    /// <summary>
    /// Idle like a human who is actually at their keyboard: fidget every 1-3s instead of every 12-35s.
    ///
    /// Exists because the biggest remaining behavioural gap between us and the vanilla open/close capture
    /// is look input — the human produced 0.809 Move Player Rot/s and 0.295 Player Input/s while spamming
    /// the NPC, where our stress arm manages 0.064 and 0.180. Every arm ejected so far has been close to
    /// inert between opens. This lets one arm open menus while *moving like the human did*.
    /// </summary>
    public async Task BusyIdleAsync(TimeSpan duration, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + duration;
        var anchor = client.State.LocalPlayer?.Entity?.Position;

        while (DateTime.UtcNow < deadline && client.IsConnected && !Intercepted && LobbyEjection is null)
        {
            var quiet = TimeSpan.FromSeconds(Random.Shared.Next(1, 4));
            var until = DateTime.UtcNow + quiet;
            if (until > deadline) until = deadline;
            while (DateTime.UtcNow < until && client.IsConnected && !Intercepted) await Task.Delay(200, ct);

            if (DateTime.UtcNow >= deadline || !client.IsConnected || Intercepted || LobbyEjection is not null) break;
            if (containers.IsContainerOpen) continue;
            await FidgetAsync(anchor, ct);
        }

        StopMoving();
    }

    private async Task FidgetAsync(Vector3<double>? anchor, CancellationToken ct)
    {
        var entity = client.State.LocalPlayer?.Entity;
        if (entity is null) return;

        var pos = entity.Position;
        var strayed = anchor is not null && pos is not null && Dist(pos, anchor) > 4.0;

        // Turn to face the anchor before stepping when we have drifted, so the steps bring us back.
        if (strayed && pos is not null && anchor is not null)
        {
            var yaw = (float)(Math.Atan2(anchor.Z - pos.Z, anchor.X - pos.X) * 180 / Math.PI) - 90f;
            entity.YawPitch = new Vector2<float>(yaw, entity.YawPitch.Y);
        }
        else
        {
            // Idle glancing about, which is what a bored player does between checks.
            var yaw = entity.YawPitch.X + (float)(Random.Shared.NextDouble() * 120 - 60);
            var pitch = Math.Clamp(entity.YawPitch.Y + (float)(Random.Shared.NextDouble() * 30 - 15), -60f, 60f);
            entity.YawPitch = new Vector2<float>(yaw, pitch);
        }

        var roll = Random.Shared.Next(100);
        var input = MinecraftProtoNet.Core.Models.Input.Input.Empty;

        if (strayed || roll < 45)
        {
            input = input with { Forward = true };
        }
        else if (roll < 60)
        {
            input = input with { Left = true };
        }
        else if (roll < 75)
        {
            input = input with { Right = true };
        }
        else if (roll < 90)
        {
            input = input with { Jump = true };
        }
        else
        {
            return; // just the look, no movement
        }

        entity.InputState.Current = input;
        await Task.Delay(Random.Shared.Next(180, 550), ct);
        entity.InputState.Current = MinecraftProtoNet.Core.Models.Input.Input.Empty;
        _lastMovementUtc = DateTime.UtcNow;
    }

    public void StopMoving()
    {
        try
        {
            baritoneProvider.CreateBaritone(client).GetPathingBehavior().CancelEverything();
        }
        catch
        {
            // Cancelling is best-effort; the input clear below is what actually keeps the bot still.
        }

        // Input is immutable per tick, so releasing everything means installing the empty one.
        if (client.State.LocalPlayer?.Entity is { } entity)
        {
            entity.InputState.Current = MinecraftProtoNet.Core.Models.Input.Input.Empty;
        }
    }

    /// <summary>
    /// Set when the server says the Bazaar is unavailable — SkyBlock disables it when a hub is under load.
    /// Cleared by <see cref="ClearOutage"/> once the caller has moved somewhere else.
    /// </summary>
    public string? OutageNotice { get; private set; }

    public void ClearOutage() => OutageNotice = null;

    /// <summary>
    /// Recognises an outage without needing Hypixel's exact wording, which we have not observed yet and which
    /// they are free to change: a line that mentions the Bazaar (or a trade verb) alongside language about
    /// being off, busy or postponed. Deliberately broad — a false positive costs one hub hop, a false negative
    /// costs a bot that keeps clicking a dead menu.
    /// </summary>
    /// <summary>
    /// True if the line was written by another player rather than by the server. Gates the notices that make
    /// the bot ACT on what it reads; see <see cref="HypixelChat"/> for why the packet type cannot tell us.
    ///
    /// Deliberately not applied to <see cref="NoteIfIntercepted"/>: that one exists to notice a human taking an
    /// interest in the bot, so player chat is exactly what it needs to hear.
    /// </summary>
    private static bool LooksLikePlayerChat(string line) => HypixelChat.IsPlayerChat(line);

    private void NoteIfOutage(string line)
    {
        if (OutageNotice is not null) return;
        if (LooksLikePlayerChat(line)) return;

        var lower = line.ToLowerInvariant();
        var mentionsBazaar = lower.Contains("bazaar") || lower.Contains("buy order") || lower.Contains("sell offer")
                             || lower.Contains("auction house");
        if (!mentionsBazaar) return;

        string[] outageWords =
        [
            "disabled", "unavailable", "temporarily", "currently closed", "is closed", "try again",
            "too busy", "high load", "server load", "overloaded", "maintenance", "not available", "failed",
            // "This server is too laggy to use the Bazaar, sorry!" -- observed 2026-08-09, and it matched
            // none of the words above, so the bot kept trying to trade on a hub that had switched the
            // Bazaar off (it fired 10+ times in one night). Same class as the others: it is per-server, so
            // the fix is to be on another server.
            //
            // Still matched on the whole phrase rather than "laggy" alone. LooksLikePlayerChat above is the
            // real defence against someone in lobby chat complaining, but the phrase costs nothing and keeps
            // this working if the prefix rule ever misses a chat format.
            "too laggy to use"
        ];

        var hit = outageWords.FirstOrDefault(w => lower.Contains(w));
        if (hit is null) return;

        OutageNotice = line;
        log($"!! BAZAAR OUTAGE NOTICE (matched \"{hit}\"): {line}");
    }

    private void OnScreenText(ScreenTextEventArgs e)
    {
        var text = ItemTextHelper.StripFormattingCodes(e.Text).Trim();
        if (text.Length == 0) return;

        // SkyBlock paints stats and pickup spam here constantly; keep the rest.
        if (text.Contains("Mana") && text.Contains("/")) return;

        var stamped = $"[{e.Kind}] {text}";
        lock (_screenText)
        {
            if (_screenText.Count < 5000) _screenText.Add(stamped);
        }

        log($"screen text {stamped}");

        // "You are AFK / Move around to return to the lobby." is Hypixel's five-minute idle warning, and it
        // is the actual mechanism behind the lobby ejections that cost this bot most of an evening — the
        // "Sending packets too fast!" text that accompanied some of them was coincidental. Position packets
        // are not input as far as Hypixel is concerned; only real movement resets the timer. Answering the
        // warning the moment it appears is the cheapest possible fix, and it is exactly what the subtitle
        // instructs a player to do.
        if (text.Contains("AFK", StringComparison.OrdinalIgnoreCase))
        {
            log("!! AFK warning — moving to reset the idle timer");
            _ = Task.Run(async () =>
            {
                try { await NudgeAsync(); }
                catch { /* the next fidget will try again */ }
            });
        }

        // A book or a dialog is never something this bot asked for. We cannot answer a challenge we have not
        // seen before — and guessing at one would be worse than stopping — so the response is to keep the
        // evidence and hand it to a human.
        if (e.Kind is ScreenTextKind.Book or ScreenTextKind.Dialog)
        {
            RaiseIntercept($"unsolicited {e.Kind.ToString().ToLowerInvariant()}",
                "the server pushed a screen the bot never requested; payload preserved in the screen-text dump above");
            return;
        }

        NoteIfIntercepted(stamped);
    }

    public List<string> ScreenTextSnapshot()
    {
        lock (_screenText) return [.. _screenText];
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

            // Joining lands us in a lobby and then moves us; none of that is worth flagging.
            ExpectRelocationFor(TimeSpan.FromSeconds(60));
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
        // A warp command is a relocation we asked for; give the watchdog a window so it does not read the
        // resulting teleport as somebody else moving us.
        if (command.StartsWith("hub", StringComparison.OrdinalIgnoreCase)
            || command.StartsWith("skyblock", StringComparison.OrdinalIgnoreCase)
            || command.StartsWith("warp", StringComparison.OrdinalIgnoreCase))
        {
            ExpectRelocationFor(TimeSpan.FromSeconds(25));
        }

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
        // Pathing produces continuous movement, never a jump, so the watchdog stays armed while walking.
        var baritone = baritoneProvider.CreateBaritone(client);
        log($"pathing to ({goal.X},{goal.Y},{goal.Z}), timeout {timeoutSec}s");
        baritone.GetCustomGoalProcess().SetGoalAndPath(new GoalNear(goal.X, goal.Y, goal.Z, 3));

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
        var nextReport = DateTime.UtcNow.AddSeconds(5);
        var bestDist = double.MaxValue;
        var progressAt = DateTime.UtcNow;
        while (DateTime.UtcNow < deadline && client.IsConnected)
        {
            // Checked every iteration, not at the end: an intercept mid-walk has to stop the walk, not finish it.
            if (Intercepted)
            {
                baritone.GetPathingBehavior().CancelEverything();
                log("walk abandoned — intercept");
                return false;
            }

            // Likewise for a kick to the lobby. The destination is in a world we are no longer in, so without
            // this the bot walks the lobby until the full timeout expires before anything notices.
            if (LobbyEjection is not null)
            {
                baritone.GetPathingBehavior().CancelEverything();
                log("walk abandoned — ejected to the lobby");
                return false;
            }

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
                // Wedged-in-geometry detector.
                //
                // The walk timeout alone is not enough: on 2026-08-08 the bot sank into the floor near the
                // Bazaar at (-33.7, 66.2, -30.2) — six blocks BELOW the walkway — and sat there while the
                // server shoved it up by dy=+0.0410 thousands of times. Baritone kept "pathing", the position
                // never changed, and 45 minutes of trading were lost with no alarm, because every failure
                // counter it has was happy. Bail out as soon as the position stops changing so the caller can
                // re-establish rather than grind out the whole timeout.
                // Measured on PROGRESS TOWARD THE GOAL, not on raw position.
                //
                // A position test misses the second failure mode: after falling off the walkway the bot sat in
                // a pit at (-40.5, 67.x, -38.4) with Y oscillating 67.2<->67.7 while making no horizontal
                // headway, so "has the position changed" kept resetting and the walk ran its full timeout.
                // Distance-to-goal collapses both cases — wedged in geometry, or bouncing somewhere it cannot
                // climb out of — into one check.
                if (dist < bestDist - 0.5)
                {
                    bestDist = dist;
                    progressAt = DateTime.UtcNow;
                }
                else if (DateTime.UtcNow - progressAt > TimeSpan.FromSeconds(25))
                {
                    baritone.GetPathingBehavior().CancelEverything();
                    log($"walk abandoned — no progress for 25s at ({pos.X:F1},{pos.Y:F1},{pos.Z:F1}), " +
                        $"still {dist:F1} from the goal (best {bestDist:F1})");
                    return false;
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
                // Nearest to the label wins, measured horizontally.
                //
                // This used to take the HIGHEST entity in a 2x2 column under the label, which in a crowded hub
                // picks whoever happens to be standing beside the NPC — and the bot then faces a bystander,
                // right-clicks them, and gets no menu. An NPC's label hangs directly over its own body, so
                // horizontal distance separates the two cleanly; the box is tightened for the same reason.
                var body = all
                    .Where(e => e.EntityId != label.EntityId
                                && LabelTextOf(e) is null
                                && Math.Abs(e.Position.X - label.Position.X) <= 0.7
                                && Math.Abs(e.Position.Z - label.Position.Z) <= 0.7
                                && label.Position.Y - e.Position.Y is >= -0.5 and <= 5.0)
                    .OrderBy(e => (e.Position.X - label.Position.X) * (e.Position.X - label.Position.X)
                                  + (e.Position.Z - label.Position.Z) * (e.Position.Z - label.Position.Z))
                    .ThenByDescending(e => e.Position.Y)
                    .FirstOrDefault();
                if (body is not null)
                {
                    _npc = body;
                    _npcName = nameSubstring;
                    // The offset is logged because it is the tell for a mis-resolution: an NPC sits under its
                    // own label at ~0.0, so anything approaching the box limit is probably a passer-by.
                    var offset = Math.Sqrt(
                        Math.Pow(body.Position.X - label.Position.X, 2) +
                        Math.Pow(body.Position.Z - label.Position.Z, 2));

                    log($"NPC \"{nameSubstring}\" is entity {body.EntityId} at " +
                        $"({body.Position.X:F1},{body.Position.Y:F1},{body.Position.Z:F1}), {offset:F2} from its label");
                    return true;
                }
            }

            await Task.Delay(250);
        }
        return false;
    }

    /// <summary>
    /// Takes a couple of steps sideways and re-closes on the NPC, which is what a player does when someone is
    /// stood in the way.
    ///
    /// Retrying an interact from the same spot cannot fix a blocked or mistaken target — it reproduces the
    /// same geometry and therefore the same wrong result. Changing where the bot stands changes which entity
    /// is nearest, which is the thing that was wrong.
    /// </summary>
    private async Task SidestepAsync(CancellationToken ct = default)
    {
        var entity = client.State.LocalPlayer?.Entity;
        if (entity is null || !client.IsConnected || Intercepted) return;

        log("stepping aside before retrying");

        var left = Random.Shared.Next(2) == 0;
        entity.InputState.Current = left
            ? MinecraftProtoNet.Core.Models.Input.Input.Empty with { Left = true }
            : MinecraftProtoNet.Core.Models.Input.Input.Empty with { Right = true };

        await Task.Delay(Random.Shared.Next(350, 700), ct);
        entity.InputState.Current = MinecraftProtoNet.Core.Models.Input.Input.Empty;
        _lastMovementUtc = DateTime.UtcNow;

        await Task.Delay(250, ct);

        // Back within reach from the new angle; the sidestep may have taken us out of range.
        await ApproachNpcAsync();
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
        while (DateTime.UtcNow < deadline && client.IsConnected && !Intercepted)
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
        if (_npc is null || Intercepted) return false;

        var sinceLastOpen = DateTime.UtcNow - _lastNpcOpen;
        if (sinceLastOpen < TimeSpan.FromMilliseconds(NpcOpenGapMs))
        {
            var wait = TimeSpan.FromMilliseconds(NpcOpenGapMs) - sinceLastOpen;
            log($"  NPC open spacing: waiting {wait.TotalSeconds:F1}s before re-opening the menu");
            await Task.Delay(wait);
        }

        await MenuGateAsync("open the NPC menu");
        _lastNpcOpen = DateTime.UtcNow;

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

            // null = let ContainerManager compute the real ray/hitbox intersection. Passing the constant
            // (0, 1, 0) here overrode that and sent an identical hit vector on every NPC right-click the
            // account ever made; a real client sends the actual cursor hit, which differs every time.
            var opened = await containers.InteractWithEntityAsync(
                _npc.EntityId,
                Hand.MainHand,
                location: null);

            if ((opened || containers.IsContainerOpen) && await WaitForMenuContentAsync(TimeSpan.FromSeconds(6)))
            {
                if (expectedTitle is null || ContainerTitle.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // A menu opened, but not the one asked for. Since interacts are addressed by entity id, the
                // usual explanation is that the NPC search resolved to a PLAYER standing by the NPC — Hypixel
                // opens a player's inventory on right-click, which arrives as an ordinary container and would
                // otherwise be clicked as though it were the Bazaar. Re-resolve and move before trying again;
                // repeating the same interact from the same spot only reopens the same stranger's bag.
                log($"opened \"{ContainerTitle}\" but wanted \"{expectedTitle}\" (attempt {attempt}) — " +
                    "probably a player in the way");

                await containers.CloseContainerAsync();
                if (_npcName.Length > 0) await FindNpcAsync(_npcName, TimeSpan.FromSeconds(8));
                await SidestepAsync();
            }
            else
            {
                log($"no menu after interact (attempt {attempt})");

                // Entity ids do not survive the server re-sending entities, which happens on its own schedule.
                // A cached id then points at nothing and every interact silently does nothing — the failure
                // that stranded a session with four live orders it could no longer manage. Re-resolve by name
                // and try again rather than assuming the NPC we found at startup is still that entity.
                if (attempt >= 2 && _npcName.Length > 0)
                {
                    log($"re-resolving \"{_npcName}\" in case the entity was replaced");
                    if (await FindNpcAsync(_npcName, TimeSpan.FromSeconds(8))) await ApproachNpcAsync();
                    await SidestepAsync();
                }
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

        // Already holding an empty slot: send nothing.
        //
        // This is called on every cycle as well as on every join and recovery, and it used to re-send
        // SetCarriedItem unconditionally. Measured against a real client through the same proxy, that put us
        // at 1.34 Set Carried Item per minute against vanilla's 0.01 — 122x, and the single loudest
        // non-human signature in our serverbound stream. A player changes hotbar slot when they want a
        // different item, not once a minute forever.
        var held = entity.Inventory.HeldSlot;
        if (held is >= 0 and <= 8 && entity.Inventory.GetSlot((short)(held + 36)).IsEmpty) return;

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
    public async Task<bool> ClickAsync(string wanted, bool waitForChange = true, sbyte button = 0)
    {
        await MenuGateAsync($"click '{wanted}'");
        var container = containers.CurrentContainer;
        if (container is null || !container.IsOpen)
        {
            log($"cannot click \"{wanted}\": no menu open");
            return false;
        }

        var containerSlots = container.Type.GetContainerSlotCount();
        var match = container.SnapshotSlots()
            .Where(kv => kv.Key < containerSlots && !kv.Value.IsEmpty)
            .Select(kv => (Index: kv.Key, Name: CleanName(kv.Value)))
            .Where(x => x.Name is not null && x.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name!.Length)
            .FirstOrDefault();

        if (match.Name is null)
        {
            log($"no slot named like \"{wanted}\" in \"{ContainerTitle}\"; present: " +
                string.Join(", ", container.SnapshotSlots()
                    .Where(kv => kv.Key < containerSlots && !kv.Value.IsEmpty && CleanName(kv.Value) is not null)
                    .OrderBy(kv => kv.Key)
                    .Select(kv => $"[{kv.Key}] {CleanName(kv.Value)}")));
            return false;
        }

        // Let the window settle before touching it. A click carries the container's StateId, and Hypixel
        // closes the GUI outright when that is stale — which is what a click sent ~1s after the menu opened,
        // while contents were still arriving, actually did.
        await Task.Delay(350);

        var signature = SignatureOf(container);
        log($"{(button == 1 ? "right-click" : "click")} [{match.Index}] \"{match.Name}\"");
        await containers.ClickSlotAsync(match.Index, ClickContainerMode.Pickup, button);

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
        await MenuGateAsync($"sign '{value}'");
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

        // Clamp OUR line to what the sign editor would have let a person type: the limit is rendered pixel
        // width (90px), enforced per keystroke, so "Enchanted Spruce Log" (114px) is a line no human can
        // produce — the client stops at "Enchanted Spruc" (86px). The prompt lines below are echoed exactly
        // as the server sent them, which is also what vanilla does with lines it did not edit.
        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/gui/screens/inventory/AbstractSignEditScreen.java:58
        var typed = MinecraftFont.TypedSignLine(value);
        if (typed != value) log($"  sign text clipped to \"{typed}\" — {MinecraftFont.Width(value)}px exceeds the 90px line");

        string[] lines =
        [
            typed,
            existing.Length > 1 ? existing[1] ?? "" : "",
            existing.Length > 2 ? existing[2] ?? "" : "",
            existing.Length > 3 ? existing[3] ?? "" : ""
        ];

        log($"sign <- \"{typed}\"");
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
                // A merchant window is content-complete as soon as it is open: its trades arrive out of band
                // in MerchantOffers, never as container slots, so the slot-fill test below can never pass for
                // a villager. Observed on the Paper rig -- 15 genuine merchant opens, every one scored "no
                // menu after interact", which tripped the caller's consecutive-failure cap and dropped the
                // connection while the loop was in fact working perfectly.
                if (current.Type == MenuType.Merchant) return true;

                var containerSlots = current.Type.GetContainerSlotCount();
                var filled = current.SnapshotSlots().Count(kv => kv.Key < containerSlots && !kv.Value.IsEmpty);
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
        $"{c.ContainerId}|{c.Title}|" + string.Join(",", c.SnapshotSlots()
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
        foreach (var (index, slot) in container.SnapshotSlots().OrderBy(kv => kv.Key))
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
