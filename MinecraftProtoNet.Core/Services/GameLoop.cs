using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Manages the main game loop that drives physics ticks at the server's tick rate.
/// </summary>
public class GameLoop(ILogger<GameLoop> logger, IHumanizer humanizer, IPacketProcessor packetProcessor) : IGameLoop
{
    private CancellationTokenSource? _cts;
    private Thread? _gameLoopThread;
    private volatile bool _isRunning;

    /// <summary>
    /// Opt-in loop diagnostic (MCPROTO_LOOP_DIAG=1). Reports any tick whose start-to-start interval overruns
    /// the target, broken down by phase and stamped with the thread it ran on. Isolated multi-tick overruns
    /// are what an anticheat sees as several ticks of movement arriving in one packet.
    /// </summary>
    private static readonly bool LoopDiagEnabled =
        Environment.GetEnvironmentVariable("MCPROTO_LOOP_DIAG") == "1";

    private static double MsSince(long startTimestamp) =>
        (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

    public bool IsRunning => _isRunning;
    public event Action<Entity>? PhysicsTick;
    
    /// <summary>
    /// Event fired before each physics tick. External systems (e.g., Baritone) can subscribe to this.
    /// </summary>
    public event Action<IMinecraftClient>? PreTick;
    
    /// <summary>
    /// Event fired after each physics tick. External systems (e.g., Baritone) can subscribe to this.
    /// </summary>
    public event Action<IMinecraftClient>? PostTick;

    public void Start(IMinecraftClient client)
    {
        if (IsRunning)
        {
            logger.LogWarning("Game loop is already running");
            return;
        }

        var preTickCount = PreTick?.GetInvocationList().Length ?? 0;
        var postTickCount = PostTick?.GetInvocationList().Length ?? 0;
        logger.LogWarning("GameLoop.Start: Starting game loop (PreTick subscribers: {PreTickCount}, PostTick subscribers: {PostTickCount})", 
            preTickCount, postTickCount);

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isRunning = true;
        _gameLoopThread = new Thread(async () =>
        {
            logger.LogInformation("Game loop started");

            // Without this the loop sleeps in 15.625ms quanta and runs at 16 TPS instead of 20.
            // See HighResolutionTimer for why this stayed hidden.
            using var timerResolution = HighResolutionTimer.Acquire();

            var stopwatch = new Stopwatch();

            // Measured rate of THIS loop, as opposed to Level.ClientTickCounter, which is synced from the
            // server's world age and therefore reports ~20 TPS even when our own loop is running slow.
            var rateWindow = Stopwatch.StartNew();
            var ticksThisWindow = 0;

            long previousTickStart = 0;
            double drainMs = 0, preTickMs = 0, physicsMs = 0, postTickMs = 0, tickEndMs = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var tickStart = Stopwatch.GetTimestamp();
                    if (LoopDiagEnabled && previousTickStart != 0)
                    {
                        var interval = (tickStart - previousTickStart) * 1000.0 / Stopwatch.Frequency;
                        var target = Math.Max(1, Math.Min(1000, client.State.Level.TickInterval));
                        if (interval > target * 1.5)
                        {
                            logger.LogInformation(
                                "[LoopDiag] tick interval {Interval:F1}ms (target {Target}) — drain={Drain:F1} " +
                                "preTick={Pre:F1} physics={Phys:F1} postTick={Post:F1} tickEnd={End:F1} " +
                                "thread={Thread} pool={IsPool} gen0={G0} gen1={G1} gen2={G2}",
                                interval, target, drainMs, preTickMs, physicsMs, postTickMs, tickEndMs,
                                Environment.CurrentManagedThreadId, Thread.CurrentThread.IsThreadPoolThread,
                                GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
                        }
                    }

                    previousTickStart = tickStart;
                    stopwatch.Restart();

                    try
                    {
                        // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/client/Minecraft.java:1247
                        // Vanilla drains all queued packets BEFORE running tick logic.
                        var phase = Stopwatch.GetTimestamp();
                        await packetProcessor.ProcessQueuedPacketsAsync();
                        drainMs = MsSince(phase);

                        // Only run tick logic + emit Play packets while actually in Play. The drain above can
                        // switch us to Configuration mid-tick (server reconfiguration / proxy backend switch);
                        // sending ClientTickEnd or movement in Configuration is an illegal-state packet → instant
                        // kick. While not in Play we idle-tick — config packets are handled inline on the read
                        // loop (MinecraftClient only enqueues to the game thread when ProtocolState == Play).
                        if (client.ProtocolState == ProtocolState.Play)
                        {
                            // Increment tick counter FIRST (matches vanilla Minecraft.java:1740)
                            client.State.Level.IncrementClientTickCounter();

                            // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/Minecraft.java pick()
                            // Compute the block hit result once per tick before PreTick.
                            // In vanilla, pick() runs before handleKeybinds and player.tick().
                            // Both Baritone's objectMouseOver check (in AttemptToPlaceABlock) and
                            // InteractAsync must use the same hit result to avoid face-mismatch bugs
                            // when the player is moving fast (e.g., mid-air parkour placement).
                            if (client.State.LocalPlayer?.HasEntity == true)
                            {
                                client.State.LocalPlayer.Entity.UpdatePickResult(client.State.Level);
                            }

                            // Invoke pre-tick hook for external systems (e.g., Baritone)
                            if (PreTick != null)
                            {
                                // Log occasionally to verify it's being called (every 100 ticks = ~5 seconds)
                                if (client.State.Level.ClientTickCounter % 100 == 0)
                                {
                                    logger.LogDebug("GameLoop: Invoking PreTick event (subscribers: {Count}, tick: {Tick})",
                                        PreTick.GetInvocationList().Length, client.State.Level.ClientTickCounter);
                                }
                                phase = Stopwatch.GetTimestamp();
                                PreTick.Invoke(client);
                                preTickMs = MsSince(phase);
                            }

                            phase = Stopwatch.GetTimestamp();
                            await client.PhysicsTickAsync(entity => PhysicsTick?.Invoke(entity));
                            physicsMs = MsSince(phase);

                            // Invoke post-tick hook for external systems (e.g., Baritone)
                            if (PostTick != null)
                            {
                                logger.LogDebug("GameLoop: Invoking PostTick event (subscribers: {Count})", PostTick.GetInvocationList().Length);
                                phase = Stopwatch.GetTimestamp();
                                PostTick.Invoke(client);
                                postTickMs = MsSince(phase);
                            }

                            // Send ClientTickEndPacket at end of tick BEFORE sleep (matches vanilla Minecraft.java:1864)
                            phase = Stopwatch.GetTimestamp();
                            await client.SendPacketAsync(new ClientTickEndPacket(), token);
                            tickEndMs = MsSince(phase);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        logger.LogWarning("Connection disposed during physics tick, stopping game loop");
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in physics tick");
                    }

                    stopwatch.Stop();

                    ticksThisWindow++;
                    if (rateWindow.ElapsedMilliseconds >= 5000)
                    {
                        var measuredTps = ticksThisWindow * 1000.0 / rateWindow.ElapsedMilliseconds;
                        var targetTps = 1000.0 / Math.Max(1, client.State.Level.TickInterval);

                        // A loop that quietly runs slow desyncs every prediction we make, and it hid for a long
                        // time because the TPS everyone read came from the server-synced tick counter. Make it
                        // complain at a level that is actually visible.
                        if (Math.Abs(measuredTps - targetTps) > targetTps * 0.1)
                        {
                            logger.LogWarning(
                                "[GameLoop] loop running at {Tps:F1} TPS, target {Target:F1} — client physics and " +
                                "packet cadence are off by {Pct:F0}%",
                                measuredTps, targetTps, Math.Abs(measuredTps - targetTps) / targetTps * 100.0);
                        }
                        else
                        {
                            logger.LogDebug("[GameLoop] measured loop rate: {Tps:F1} TPS", measuredTps);
                        }

                        ticksThisWindow = 0;
                        rateWindow.Restart();
                    }

                    var targetDelayMs = Math.Max(1, Math.Min(1000, client.State.Level.TickInterval));

                    // Sub-millisecond elapsed time: truncating to whole milliseconds throws away up to 1ms per
                    // tick, which at 20 TPS is a 2% rate error on its own.
                    var processingTimeMs = stopwatch.Elapsed.TotalMilliseconds;

                    if (processingTimeMs < targetDelayMs)
                    {
                        var remainingDelayMs = targetDelayMs - processingTimeMs + humanizer.GetTickJitterMs();
                        remainingDelayMs = Math.Max(1, remainingDelayMs);
                        await Task.Delay(TimeSpan.FromMilliseconds(remainingDelayMs), token);
                    }
                    else
                    {
                        await Task.Yield();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            finally
            {
                _isRunning = false;
                logger.LogInformation("Game loop stopped");
            }
        })
        {
            Name = "GameLoop",
            IsBackground = true
        };

        // Register the game thread identity before starting so packets begin queueing immediately
        packetProcessor.SetGameThread(_gameLoopThread);
        _gameLoopThread.Start();
    }

    public async Task StopAsync()
    {
        if (_cts == null || !_isRunning)
        {
            return;
        }

        logger.LogInformation("Stopping game loop...");
        packetProcessor.Close();
        await _cts.CancelAsync();

        // Give the loop a moment to observe cancellation and exit
        await Task.Delay(200);

        _isRunning = false;
        _cts.Dispose();
        _cts = null;
        _gameLoopThread = null;
    }
}
