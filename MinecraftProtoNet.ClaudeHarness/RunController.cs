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
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>
/// Drives a scenario end-to-end with no manual input: authenticate → connect → wait spawn →
/// teleport to start → issue the goal → monitor for a terminal outcome → write artifacts → disconnect.
/// </summary>
public sealed class RunController(IMinecraftClient client, IBaritoneProvider baritoneProvider, IGameLoop gameLoop)
{
    private static void Log(string msg) => Console.WriteLine($"[harness] {msg}");

    public async Task<RunOutcome> RunAsync(Scenario scenario, string runDir, (int X, int Y, int Z)? diagPos = null)
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
            // ----- Arrange: teleport to the parkour start and let physics settle -----
            await SendCommandAsync($"tp @s {scenario.Start.X} {scenario.Start.Y} {scenario.Start.Z}");
            Log($"sent /tp to start {scenario.Start}");

            if (!await WaitAtStartAsync(scenario))
            {
                Log("FAILED to settle at start within timeout");
                recorder.ForceTerminal(RunOutcome.Error, "never settled at start position after /tp");
                outcome = await recorder.Completion;
            }
            else
            {
                // ----- Act: capture telemetry and issue the goal -----
                captureStartTick = client.State.Level.ClientTickCounter;
                recorder.BeginCapture(captureStartTick);
                baritone.GetCustomGoalProcess().SetGoalAndPath(new GoalBlock(scenario.End.X, scenario.End.Y, scenario.End.Z));
                Log($"goal set; monitoring (timeout {scenario.RunTimeoutSec}s)");

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
