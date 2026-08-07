using System.Reflection;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Utils.Pathing;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>
/// Drives a scenario end-to-end with no manual input: authenticate → connect → wait spawn →
/// teleport to start → issue the goal → monitor for a terminal outcome → write artifacts → disconnect.
/// </summary>
public sealed class RunController(
    IMinecraftClient client,
    IBaritoneProvider baritoneProvider,
    IGameLoop gameLoop,
    IContainerManager? containers = null,
    IItemRegistryService? itemRegistry = null)
{
    private static void Log(string msg) => Console.WriteLine($"[harness] {msg}");

    public async Task<RunOutcome> RunAsync(Scenario scenario, string runDir, (int X, int Y, int Z)? diagPos = null, bool clearCourse = false, bool buildOnly = false, int settleSec = 0)
    {
        Log($"scenario={scenario.Name} server={scenario.Server}:{scenario.Port} start={scenario.Start} end={scenario.End}");

        if (!await client.AuthenticateAsync())
        {
            Log("AUTH FAILED — ensure an account is added/active (run the web app once to device-code login).");
            return RunOutcome.Error;
        }
        Log("authenticated");

        if (!await ConnectAndSpawnAsync(scenario))
        {
            Log("CONNECT/SPAWN FAILED (see disconnect reason above)");
            await TeardownAsync();
            return RunOutcome.Error;
        }
        Log("connected + spawned, game loop ticking");

        var baritone = baritoneProvider.CreateBaritone(client);

        // Build-only mode: construct/restore the course and leave, so it can be inspected before the bot runs.
        if (buildOnly)
        {
            if (scenario.BuildCommands is { Count: > 0 } buildCmds)
            {
                foreach (var cmd in buildCmds)
                {
                    await SendCommandAsync(cmd);
                }
                Log($"course '{scenario.Name}' built ({buildCmds.Count} commands) — start {scenario.Start}, goal {scenario.End}");
            }
            else
            {
                Log($"scenario '{scenario.Name}' has no BuildCommands; nothing to build");
            }
            await TeardownAsync();
            return RunOutcome.Success;
        }

        // Diagnostic mode: tp to a position and log every movement's cost from there (find why pathing stalls).
        if (diagPos is { } dp)
        {
            await DiagnoseMovesAsync(baritone, dp);
            await TeardownAsync();
            return RunOutcome.Error; // diag isn't a pass/fail run
        }

        using var recorder = new TelemetryRecorder(baritone, scenario, runDir);
        Action<IMinecraftClient> tickHandler = recorder.OnGameLoopTick;
        gameLoop.PostTick += tickHandler;
        baritone.GetGameEventHandler().RegisterEventListener(recorder);

        RunOutcome outcome;
        long captureStartTick = 0;
        try
        {
            // ----- Arrange: reset the world FIRST, then teleport in -----
            // Order matters: a rebuild starts by air-filling the whole course volume, which includes the
            // ground under the start position. Teleporting first meant the bot could fall through the floor
            // in the window before the platform was re-placed (seen as an instant Fall, 12 ticks, y73 -> y64).
            // Only normalise the INVENTORY (bot must have throwaway blocks to place — "dirt only", matching the
            // reference Baritone run). We deliberately DO NOT clear the course: leftover dirt from previous runs
            // must be handled by the bot itself (re-path over / dig / build around), exactly as Baritone would.
            // Clearing the course (/fill) masks that robustness and makes every run an identical pristine course.
            // Methodology: only the FIRST run of a batch clears the course (establish a clean baseline that
            // must pass); subsequent runs leave the deltas so we test robustness to leftover blocks. --clear-course
            // removes only the bot's placed dirt (bedrock course + lava untouched; ~24k-block box < 32768 /fill limit).
            if (clearCourse)
            {
                if (scenario.BuildCommands is { Count: > 0 } build)
                {
                    // Restore the course to its default state by rebuilding it. Required for courses with
                    // breakable blocks: a dirt-only fill would leave the bot's mined-out holes in place.
                    foreach (var cmd in build)
                    {
                        await SendCommandAsync(cmd);
                    }
                    Log($"course REBUILT to default state ({build.Count} commands)");
                }
                else
                {
                    foreach (var (x1, y1, z1, x2, y2, z2) in scenario.ClearBoxes)
                    {
                        await SendCommandAsync($"fill {x1} {y1} {z1} {x2} {y2} {z2} minecraft:air replace minecraft:dirt");
                    }
                    Log($"cleared placed dirt in {scenario.ClearBoxes.Count} box(es) (clean baseline run)");
                }
            }

            // Now that the world is in its default state, put the bot on the start block.
            await SendCommandAsync($"tp @s {scenario.Start.X} {scenario.Start.Y} {scenario.Start.Z}");
            Log($"sent /tp to start {scenario.Start}");

            await SendCommandAsync("clear @s");
            var inventory = scenario.Inventory is { Count: > 0 }
                ? scenario.Inventory
                : ["give @s minecraft:dirt 64"];
            foreach (var cmd in inventory)
            {
                await SendCommandAsync(cmd);
            }
            Log($"inventory normalised ({inventory.Count} item stack(s)); course {(clearCourse ? "RESET" : "NOT reset — deltas left in place")}");

            if (!await WaitAtStartAsync(scenario))
            {
                Log("FAILED to settle at start within timeout");
                recorder.ForceTerminal(RunOutcome.Error, "never settled at start position after /tp");
                outcome = await recorder.Completion;
            }
            else
            {
                // Dump inventory — bridging/pillaring parkour needs throwaway blocks. If empty, the
                // pathfinder may plan place-routes the bot can't execute (jumps into gaps → falls).
                var inv = client.State.LocalPlayer?.Entity?.Inventory;
                if (inv != null)
                {
                    var nonEmpty = inv.Items.Where(kv => kv.Value.ItemId is > 0 && kv.Value.ItemCount > 0)
                        .Select(kv => $"slot{kv.Key}=item{kv.Value.ItemId}x{kv.Value.ItemCount}").ToList();
                    Log($"inventory: {(nonEmpty.Count == 0 ? "EMPTY" : string.Join(", ", nonEmpty))}");
                    Log($"HasThrowawayBlocks={inv.HasThrowawayBlocks()}");
                }

                // Wait for the world around the start to actually EXIST before pathing. The pathfinder reads
                // unloaded chunks as empty, so issuing a goal too early produces a route computed against a
                // world that is not there yet - seen as parkour1 planning 56 movements heading away from the
                // goal and going Stuck. This is a deterministic readiness check, not a sleep; a fixed delay
                // only ever approximated it.
                await WaitForChunksAsync(scenario.Start, radius: 3, timeout: TimeSpan.FromSeconds(20));

                // Final gate before pathing: the bot must actually BE at the start with its inventory stocked.
                // Setup commands are chat commands and the server can still be working through them; one run
                // began with the bot 30 blocks away and an empty inventory, then "failed" after a single tick.
                // That is a harness false negative, not bot behaviour, so re-issue and re-check rather than
                // measuring a run that never started properly.
                var startConfirmed = await EnsureAtStartAsync(scenario, inventory);

                // Optional extra idle on top, for deliberately probing join-time behaviour (--settle-sec).
                if (startConfirmed && settleSec > 0)
                {
                    Log($"settling for a further {settleSec}s before issuing the goal");
                    await Task.Delay(TimeSpan.FromSeconds(settleSec));
                }

                // ----- Act: capture telemetry and issue the goal -----
                captureStartTick = client.State.Level.ClientTickCounter;
                recorder.BeginCapture(captureStartTick);

                if (!startConfirmed)
                {
                    Log("FAILED to confirm start state (position/inventory) before issuing the goal");
                    recorder.ForceTerminal(RunOutcome.Error, "start state not confirmed before goal");
                }
                else if (scenario.Name == "villager1")
                {
                    // Task-shaped scenario: the objective is an inventory outcome, not reaching a block, so it
                    // drives Baritone itself rather than being handed a single goal.
                    if (containers == null || itemRegistry == null)
                    {
                        recorder.ForceTerminal(RunOutcome.Error, "villager task needs IContainerManager + IItemRegistryService");
                    }
                    else
                    {
                        var task = new VillagerTask(client, baritone, containers, itemRegistry, SendCommandAsync);
                        var (ok, detail) = await task.RunAsync("minecraft:emerald", TimeSpan.FromSeconds(scenario.RunTimeoutSec));
                        recorder.ForceTerminal(ok ? RunOutcome.Success : RunOutcome.Stuck, detail);
                    }
                }
                else
                {
                    baritone.GetCustomGoalProcess().SetGoalAndPath(new GoalBlock(scenario.End.X, scenario.End.Y, scenario.End.Z));
                    Log($"goal set; monitoring (timeout {scenario.RunTimeoutSec}s)");
                }

                // Poll for a terminal: recorder verdict, mid-run disconnect, or wall-clock timeout. Detecting
                // disconnect here ends the run immediately instead of waiting out the full timeout.
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(scenario.RunTimeoutSec));
                while (!recorder.Completion.IsCompleted)
                {
                    await Task.WhenAny(recorder.Completion, Task.Delay(250));
                    if (recorder.Completion.IsCompleted) break;
                    if (!client.IsConnected)
                    {
                        recorder.ForceTerminal(RunOutcome.Error, "client disconnected mid-run");
                        break;
                    }
                    if (timeoutTask.IsCompleted)
                    {
                        recorder.ForceTerminal(RunOutcome.Timeout, $"exceeded {scenario.RunTimeoutSec}s wall-clock");
                        break;
                    }
                }
                outcome = await recorder.Completion;
            }

            long durationTicks = (recorder.TerminalSample?.Tick ?? captureStartTick) - captureStartTick;
            Log($"OUTCOME={outcome} :: {recorder.TerminalDetail}");
            ReportWriter.Write(runDir, scenario, outcome, recorder, baritone, durationTicks);
        }
        finally
        {
            gameLoop.PostTick -= tickHandler;
            // "Kill the bot" — disconnect (also halts the game loop, which stops pathing).
            await TeardownAsync();
        }

        return outcome;
    }

    // Connect AND wait for spawn in one retry loop. ConnectAsync returns before the async login flow
    // completes, so a login disconnect (notably the transient `unverified_username` Mojang race) only
    // surfaces while we wait for spawn. On disconnect we back off and retry the whole connect — a new
    // handshake re-runs joinServer with a fresh serverId, which clears the race.
    private async Task<bool> ConnectAndSpawnAsync(Scenario scenario)
    {
        const int maxAttempts = 6;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            client.State.LastDisconnectTranslateKey = null;
            client.State.LastDisconnectReason = null;
            await client.ConnectAsync(scenario.Server, scenario.Port, false);

            var deadline = DateTime.UtcNow.AddSeconds(12);
            long lastTick = -1;
            while (DateTime.UtcNow < deadline)
            {
                if (!client.IsConnected) break; // login disconnect (e.g. unverified_username) or drop
                if (client.State.LocalPlayer.HasEntity)
                {
                    long tick = client.State.Level.ClientTickCounter;
                    if (lastTick >= 0 && tick > lastTick) return true; // spawned + loop ticking
                    lastTick = tick;
                }
                await Task.Delay(250);
            }

            if (client.IsConnected && client.State.LocalPlayer.HasEntity) return true;

            var reason = client.State.LastDisconnectTranslateKey
                         ?? client.State.LastDisconnectReason
                         ?? "no spawn within 12s";
            Log($"connect attempt {attempt}/{maxAttempts} did not reach spawn ({reason}); backing off...");
            try { await client.DisconnectAsync(); } catch { /* best-effort */ }
            if (attempt < maxAttempts) await Task.Delay(3000);
        }
        return false;
    }

    /// <summary>
    /// Confirms the bot is genuinely at the start with the expected inventory, re-issuing the setup commands if
    /// not. Setup is done with chat commands, and the server can still be executing them while the harness moves
    /// on — one parkour1 run started with the bot 30 blocks away and an empty inventory and "failed" after a
    /// single tick, which measured nothing except the race.
    /// </summary>
    private async Task<bool> EnsureAtStartAsync(Scenario scenario, IReadOnlyList<string> inventory)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var entity = client.State.LocalPlayer?.Entity;
            var atStart = false;
            if (entity != null)
            {
                var pos = entity.Position;
                atStart = Math.Abs(pos.X - (scenario.Start.X + 0.5)) < 1.5
                          && Math.Abs(pos.Z - (scenario.Start.Z + 0.5)) < 1.5
                          && Math.Abs(pos.Y - scenario.Start.Y) < 1.5;
            }

            // Only scenarios that were given items need a stocked inventory; "clear @s" alone is legitimate.
            var wantsItems = inventory.Any(c => c.Contains("give ", StringComparison.OrdinalIgnoreCase));
            var hasItems = entity?.Inventory?.Items.Any(kv => kv.Value.ItemId is > 0 && kv.Value.ItemCount > 0) == true;
            var stocked = !wantsItems || hasItems;

            if (atStart && stocked)
            {
                if (attempt > 1) Log($"start state confirmed on attempt {attempt}");
                return true;
            }

            Log($"start state not ready (attempt {attempt}/{maxAttempts}): atStart={atStart} stocked={stocked}; re-issuing setup");
            await SendCommandAsync($"tp @s {scenario.Start.X} {scenario.Start.Y} {scenario.Start.Z}");
            foreach (var cmd in inventory)
            {
                await SendCommandAsync(cmd);
            }
            await Task.Delay(1000);
        }

        return false;
    }

    /// <summary>
    /// Blocks until every chunk within <paramref name="radius"/> of <paramref name="around"/> is loaded, so the
    /// pathfinder plans against a complete world. Logs and continues on timeout rather than failing the run —
    /// a partially loaded world is worth reporting, but it is the pathing result we are here to measure.
    /// </summary>
    private async Task WaitForChunksAsync((int X, int Y, int Z) around, int radius, TimeSpan timeout)
    {
        var centreX = around.X >> 4;
        var centreZ = around.Z >> 4;
        var expected = (radius * 2 + 1) * (radius * 2 + 1);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var loaded = 0;
            for (var cx = centreX - radius; cx <= centreX + radius; cx++)
            {
                for (var cz = centreZ - radius; cz <= centreZ + radius; cz++)
                {
                    if (client.State.Level.HasChunk(cx, cz)) loaded++;
                }
            }

            if (loaded == expected)
            {
                Log($"world ready: {loaded}/{expected} chunks loaded around {around}");
                return;
            }

            await Task.Delay(100);
        }

        Log($"WARNING: timed out waiting for chunks around {around}; pathing may plan against an incomplete world");
    }

    private async Task<bool> WaitAtStartAsync(Scenario scenario)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (!client.IsConnected) return false; // disconnected during arrange
            if (client.State.LocalPlayer.HasEntity)
            {
                var p = client.State.LocalPlayer.Entity.Position;
                bool near = Math.Abs(p.X - (scenario.Start.X + 0.5)) < 1.5
                            && Math.Abs(p.Z - (scenario.Start.Z + 0.5)) < 1.5
                            && Math.Abs(p.Y - scenario.Start.Y) < 1.5;
                if (near && client.State.LocalPlayer.Entity.IsOnGround)
                {
                    await Task.Delay(500); // let physics settle a few ticks
                    return true;
                }
            }
            await Task.Delay(200);
        }
        return false;
    }

    private async Task SendCommandAsync(string command)
    {
        // Mirror CmdCommand: signed command packet if the server enforces secure chat, else plain. SendPacketAsync
        // isn't on IMinecraftClient, so use the concrete MinecraftClient (that's what DI registers).
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

    // Diagnostic: log every movement's destination + cost from a given position, to see which continuations
    // the pathfinder considers valid vs CostInf (used to find why pathing stalls at a spot real Baritone passes).
    private async Task DiagnoseMovesAsync(IBaritone baritone, (int X, int Y, int Z) pos)
    {
        await SendCommandAsync($"tp @s {pos.X} {pos.Y} {pos.Z}");
        Log($"[diag] tp to {pos}; settling...");
        await Task.Delay(2500); // let chunks load + physics settle

        var nameMap = typeof(Moves)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Moves.MoveType))
            .ToDictionary(f => (Moves.MoveType)f.GetValue(null)!, f => f.Name);

        var ctx = new CalculationContext(baritone);
        // The cost model's own limits — a dry fall taller than MaxFallHeightNoWater+1 must be CostInf, so if
        // a long fall is being costed finitely these are the first thing to check.
        Log($"[diag] context: MaxFallHeightNoWater={ctx.MaxFallHeightNoWater} MaxFallHeightBucket={ctx.MaxFallHeightBucket} " +
            $"MinFallHeight={ctx.MinFallHeight} HasWaterBucket={ctx.HasWaterBucket} AllowFallIntoLava={ctx.AllowFallIntoLava} " +
            $"AllowParkour={ctx.AllowParkour} HasThrowaway={ctx.HasThrowaway}");
        Log($"[diag] movement costs from ({pos.X},{pos.Y},{pos.Z}):");
        foreach (var mt in Moves.Values)
        {
            var res = new MutableMoveResult();
            res.Reset();
            string label = nameMap.TryGetValue(mt, out var n) ? n : $"({mt.XOffset},{mt.YOffset},{mt.ZOffset})";
            try
            {
                mt.Apply(ctx, pos.X, pos.Y, pos.Z, res);
                string cost = res.Cost >= ActionCosts.CostInf ? "INF" : res.Cost.ToString("F1");
                Log($"[diag]   {label,-18} -> ({res.X},{res.Y},{res.Z}) cost={cost}");
            }
            catch (Exception ex)
            {
                Log($"[diag]   {label,-18} EXCEPTION {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Dump the nearby blocks (non-air) so the geometry is visible: the column the course descends through.
        Log($"[diag] nearby non-air blocks (x {pos.X-3}..{pos.X+3}, y {pos.Y-10}..{pos.Y+2}, z {pos.Z-3}..{pos.Z+10}):");
        for (int by = pos.Y + 2; by >= pos.Y - 10; by--)
        {
            for (int bx = pos.X - 3; bx <= pos.X + 3; bx++)
            {
                for (int bz = pos.Z - 3; bz <= pos.Z + 10; bz++)
                {
                    var bs = ctx.Get(bx, by, bz);
                    if (bs != null && !bs.IsAir)
                    {
                        Log($"[diag]   ({bx},{by},{bz}) = {bs.Name}");
                    }
                }
            }
        }
    }

    private async Task TeardownAsync()
    {
        try { await client.DisconnectAsync(); } catch { /* best-effort */ }
        try { await gameLoop.StopAsync(); } catch { /* best-effort */ }
    }
}
