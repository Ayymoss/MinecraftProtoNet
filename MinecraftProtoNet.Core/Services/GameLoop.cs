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
/// Drives the client the way vanilla does: a FRAME loop that drains the inbound packet queue once per
/// frame and then runs however many 50 ms game ticks have accumulated since the last frame.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/Minecraft.java:855-864 (the render loop)
///            and :1127-1145 (runTick).
///
/// This used to be a bare tick loop — one tick every 50 ms, drain included. That produced the right
/// AVERAGE rate but the wrong DISTRIBUTION, and the distribution is what is visible on the wire. Measured
/// against a real client idling on Hypixel for 27 minutes: vanilla's ClientTickEnd interval had a standard
/// deviation of 23.72 ms, ours 1.8 ms. Vanilla's common intervals were 67 ms, 33 ms and — 2244 times — 1 ms,
/// i.e. two ticks emitted inside the same millisecond because one frame ran two ticks. No real client is a
/// metronome, because no real client's ticks are paced by a timer; they are paced by frames.
/// </summary>
public class GameLoop(ILogger<GameLoop> logger, IHumanizer humanizer, IPacketProcessor packetProcessor) : IGameLoop
{
    private CancellationTokenSource? _cts;
    private Thread? _gameLoopThread;
    private volatile bool _isRunning;

    /// <summary>
    /// Opt-in loop diagnostic (MCPROTO_LOOP_DIAG=1). Reports any frame that runs more ticks than expected,
    /// broken down by phase and stamped with the thread it ran on.
    /// </summary>
    private static readonly bool LoopDiagEnabled =
        Environment.GetEnvironmentVariable("MCPROTO_LOOP_DIAG") == "1";

    /// <summary>
    /// Target frame rate. Vanilla's tick emission pattern is a function of its FRAME rate, so this is the
    /// knob that decides whether we look like a real client or not.
    ///
    /// The default of 30 is not arbitrary: the reference capture was a client sitting in the background,
    /// which is what a headless bot most resembles, and ~33 ms frames against 50 ms ticks is exactly what
    /// generates the observed 33/67 ms alternation (two frames per tick, then one frame that carries the
    /// tick over) plus the occasional same-millisecond pair. Running this at 60 would produce a near-regular
    /// 50 ms and would NOT match the control.
    /// </summary>
    private static readonly double TargetFrameRate =
        double.TryParse(Environment.GetEnvironmentVariable("MCPROTO_FRAME_RATE"), out var fps) && fps is >= 5 and <= 300
            ? fps
            : 30.0;

    /// <summary>
    /// Half-width of the per-frame timing noise, in ms. Deliberately SMALL: the vanilla control's tick
    /// intervals are sharply bimodal (67ms x7940, 33ms x7415), which only survives if the frame period is
    /// fairly stable. Wide continuous jitter smears those two modes into a hump, which is a different
    /// distribution even when the stdev happens to match.
    /// </summary>
    private const double FrameJitterMs = 1.5;

    /// <summary>
    /// Occasional long-frame spike (GC, chunk mesh, disk), in ms. This is what produces the control's 2244
    /// same-millisecond tick pairs: a frame has to run past ~100 ms before it owes two ticks at once, and no
    /// amount of small jitter around a 33 ms frame can ever do that.
    /// </summary>
    private const double FrameSpikeMs = 95.0;

    /// <summary>Probability that a given frame takes a spike.</summary>
    private const double FrameSpikeChance = 0.05;

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
        _gameLoopThread = new Thread(() =>
        {
            // Run the whole loop under a single-threaded pump so every `await` continuation comes back to
            // THIS thread.
            //
            // The thread body used to be `async (...)`, i.e. async void with no SynchronizationContext. After
            // the first await, every subsequent iteration resumed on a thread-pool thread instead: the game
            // thread we registered was abandoned within one tick. Two consequences, both real —
            // PacketProcessor.IsSameThread() compared against a thread nothing ran on and returned false for
            // ever (silently disabling the Baritone thread-safety check), and each tick's start inherited
            // thread-pool scheduling latency on top of the timer's own error.
            //
            // Vanilla's PacketProcessor is bound to a genuinely stable render thread
            // (Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/util/thread/PacketProcessor.java:18-24).
            SingleThreadedSynchronizationContext.Run(() => RunAsync(client, token));
        })
        {
            Name = "GameLoop",
            IsBackground = true
        };

        // Register the game thread identity before starting so packets begin queueing immediately
        packetProcessor.SetGameThread(_gameLoopThread);
        _gameLoopThread.Start();
    }

    private async Task RunAsync(IMinecraftClient client, CancellationToken token)
    {
        logger.LogInformation("Game loop started (frame loop @ {Fps:F0} fps)", TargetFrameRate);

        // Without this the loop sleeps in 15.625ms quanta and runs far below target.
        // See HighResolutionTimer for why this stayed hidden.
        using var timerResolution = HighResolutionTimer.Acquire();

        // Measured rate of THIS loop, as opposed to Level.ClientTickCounter.
        var rateWindow = Stopwatch.StartNew();
        var ticksThisWindow = 0;
        var framesThisWindow = 0;

        double drainMs = 0, preTickMs = 0, physicsMs = 0, postTickMs = 0, tickEndMs = 0;

        // ClientTickEnd emission gaps, for the distribution check under MCPROTO_LOOP_DIAG.
        var emitGaps = new List<double>();
        long lastEmitTimestamp = 0;

        var frameNoise = new Random();

        // Vanilla's DeltaTracker.Timer residual accumulator.
        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/DeltaTracker.java:33-40
        //   deltaTicks = (currentMs - lastMs) / targetMspt;
        //   deltaTickResidual += deltaTicks;
        //   int ticks = (int)deltaTickResidual;
        //   deltaTickResidual -= ticks;
        // Carrying the fractional remainder is what makes the long-run tick rate exact while letting any
        // individual frame run 0, 1 or 2+ ticks. Held as a double here; vanilla uses float, and the extra
        // precision only removes drift we do not want.
        double tickResidual = 0;
        var lastFrameTimestamp = Stopwatch.GetTimestamp();

        // Absolute schedule for the next FRAME. Pinning to an absolute instant rather than sleeping for
        // (target - work) is what stops the period drifting; see the long note in the wait block below.
        var nextFrameDeadline = Stopwatch.GetTimestamp();
        var frameIntervalTicks = (long)(1000.0 / TargetFrameRate / 1000.0 * Stopwatch.Frequency);

        try
        {
            while (!token.IsCancellationRequested)
            {
                framesThisWindow++;

                // ---- Frame: drain inbound FIRST, exactly once ----
                // Reference: Minecraft.java:1131-1132 — processQueuedPackets() is the first thing runTick does,
                // and runTick is called once per frame from the render loop at :864.
                //
                // Draining per TICK instead of per FRAME was the direct cause of the metronome: every reply we
                // generate from an inbound packet (Pong, KeepAlive, ChunkBatchReceived) could only leave at a
                // tick boundary, in the same phase as ClientTickEnd and movement. Vanilla's replies are
                // decoupled from the tick, which is why a real client answers Ping in a 7 ms median while ours
                // was drawn from a flat 0-50 ms tick-quantised distribution.
                var phase = Stopwatch.GetTimestamp();
                try
                {
                    await packetProcessor.ProcessQueuedPacketsAsync();
                }
                catch (ObjectDisposedException)
                {
                    logger.LogWarning("Connection disposed during packet drain, stopping game loop");
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error draining queued packets");
                }
                drainMs = MsSince(phase);

                // ---- How many game ticks does this frame owe? ----
                var frameNow = Stopwatch.GetTimestamp();
                var elapsedMs = (frameNow - lastFrameTimestamp) * 1000.0 / Stopwatch.Frequency;
                lastFrameTimestamp = frameNow;

                var targetMspt = Math.Max(1, Math.Min(1000, client.State.Level.TickInterval));
                tickResidual += elapsedMs / targetMspt;
                var ticksToDo = (int)tickResidual;
                tickResidual -= ticksToDo;

                // Reference: Minecraft.java:1144 — `for (int i = 0; i < Math.min(10, ticksToDo); ++i)`.
                // Vanilla never replays more than 10 ticks after a stall; the rest is dropped, not caught up.
                var ticksToRun = Math.Min(10, ticksToDo);

                if (LoopDiagEnabled && ticksToRun > 2)
                {
                    logger.LogInformation(
                        "[LoopDiag] frame ran {Ticks} ticks ({Elapsed:F1}ms elapsed, target {Target}ms) — " +
                        "drain={Drain:F1} preTick={Pre:F1} physics={Phys:F1} postTick={Post:F1} tickEnd={End:F1} " +
                        "thread={Thread} pool={IsPool} gen0={G0} gen1={G1} gen2={G2}",
                        ticksToRun, elapsedMs, targetMspt, drainMs, preTickMs, physicsMs, postTickMs, tickEndMs,
                        Environment.CurrentManagedThreadId, Thread.CurrentThread.IsThreadPoolThread,
                        GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
                }

                var stopped = false;
                for (var i = 0; i < ticksToRun && !token.IsCancellationRequested; i++)
                {
                    try
                    {
                        // Only run tick logic + emit Play packets while actually in Play. The drain above can
                        // switch us to Configuration mid-frame (server reconfiguration / proxy backend switch);
                        // sending ClientTickEnd or movement in Configuration is an illegal-state packet →
                        // instant kick. While not in Play we idle-tick — config packets are handled inline on
                        // the read loop (MinecraftClient only enqueues to the game thread when
                        // ProtocolState == Play).
                        if (client.ProtocolState != ProtocolState.Play)
                        {
                            continue;
                        }

                        ticksThisWindow++;

                        // Increment tick counter FIRST (matches vanilla Minecraft.java:1722)
                        client.State.Level.IncrementClientTickCounter();

                        // Reference: Minecraft.java pick()
                        // Compute the block hit result once per tick before PreTick. In vanilla, pick() runs
                        // before handleKeybinds and player.tick(). Both Baritone's objectMouseOver check (in
                        // AttemptToPlaceABlock) and InteractAsync must use the same hit result to avoid
                        // face-mismatch bugs when the player is moving fast.
                        if (client.State.LocalPlayer?.HasEntity == true)
                        {
                            client.State.LocalPlayer.Entity.UpdatePickResult(client.State.Level);
                        }

                        if (PreTick != null)
                        {
                            if (client.State.Level.ClientTickCounter % 100 == 0)
                            {
                                logger.LogDebug("GameLoop: Invoking PreTick event (subscribers: {Count}, tick: {Tick})",
                                    PreTick.GetInvocationList().Length, client.State.Level.ClientTickCounter);
                            }
                            phase = Stopwatch.GetTimestamp();
                            PreTick.Invoke(client);
                            preTickMs = MsSince(phase);
                        }

                        // Vanilla's handleKeybinds() slot: interactions go out HERE, before the movement
                        // packet this tick will send. Anything queued off-thread lands in the right place in
                        // the stream instead of after the movement packet, which GrimAC flags as
                        // "Post: interact entity".
                        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/Minecraft.java
                        //   pick() -> handleKeybinds() -> player.tick() -> sendPosition()
                        await client.DrainPreMovementActionsAsync();

                        phase = Stopwatch.GetTimestamp();
                        await client.PhysicsTickAsync(entity => PhysicsTick?.Invoke(entity));
                        physicsMs = MsSince(phase);

                        if (PostTick != null)
                        {
                            logger.LogDebug("GameLoop: Invoking PostTick event (subscribers: {Count})", PostTick.GetInvocationList().Length);
                            phase = Stopwatch.GetTimestamp();
                            PostTick.Invoke(client);
                            postTickMs = MsSince(phase);
                        }

                        // Send ClientTickEndPacket at end of tick (matches vanilla Minecraft.java:1806-1809)
                        phase = Stopwatch.GetTimestamp();
                        await client.SendPacketAsync(new ClientTickEndPacket(), token);
                        tickEndMs = MsSince(phase);

                        // The whole point of the frame loop is the SHAPE of this stream, so measure it
                        // directly rather than inferring it. A real client idling on Hypixel for 27 minutes
                        // produced a ClientTickEnd interval stdev of 23.72 ms; the old fixed-rate tick loop
                        // produced 1.8 ms. Mean alone cannot tell the two apart — both average 50 ms.
                        if (LoopDiagEnabled)
                        {
                            var emitNow = Stopwatch.GetTimestamp();
                            if (lastEmitTimestamp != 0)
                            {
                                var gapMs = (emitNow - lastEmitTimestamp) * 1000.0 / Stopwatch.Frequency;
                                emitGaps.Add(gapMs);

                                if (emitGaps.Count >= 300)
                                {
                                    var mean = emitGaps.Average();
                                    var stdev = Math.Sqrt(emitGaps.Sum(g => (g - mean) * (g - mean)) / emitGaps.Count);
                                    var buckets = emitGaps
                                        .GroupBy(g => (int)Math.Round(g))
                                        .OrderByDescending(gr => gr.Count())
                                        .Take(6)
                                        .Select(gr => $"{gr.Key}ms x{gr.Count()}");
                                    // Console.Error, not the logger: this category's Information level is
                                    // filtered in the harness, and a diagnostic you cannot see is worthless.
                                    Console.Error.WriteLine(
                                        $"[TickEmit] n={emitGaps.Count} mean={mean:F2}ms stdev={stdev:F2}ms " +
                                        $"(vanilla control: mean 50.0, stdev 23.72) top: {string.Join(", ", buckets)}");
                                    emitGaps.Clear();
                                }
                            }
                            lastEmitTimestamp = emitNow;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        logger.LogWarning("Connection disposed during physics tick, stopping game loop");
                        stopped = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in physics tick");
                    }
                }

                if (stopped) break;

                if (rateWindow.ElapsedMilliseconds >= 5000)
                {
                    var measuredTps = ticksThisWindow * 1000.0 / rateWindow.ElapsedMilliseconds;
                    var measuredFps = framesThisWindow * 1000.0 / rateWindow.ElapsedMilliseconds;
                    var targetTps = 1000.0 / Math.Max(1, client.State.Level.TickInterval);

                    // A loop that quietly runs slow desyncs every prediction we make, and it hid for a long
                    // time because the TPS everyone read came from the server-synced tick counter. Only warn
                    // when we were actually in Play for the window — an idle Configuration stretch legitimately
                    // runs zero ticks.
                    if (client.ProtocolState == ProtocolState.Play && ticksThisWindow > 0
                        && Math.Abs(measuredTps - targetTps) > targetTps * 0.1)
                    {
                        logger.LogWarning(
                            "[GameLoop] loop running at {Tps:F1} TPS ({Fps:F1} fps), target {Target:F1} — client " +
                            "physics and packet cadence are off by {Pct:F0}%",
                            measuredTps, measuredFps, targetTps, Math.Abs(measuredTps - targetTps) / targetTps * 100.0);
                    }
                    else
                    {
                        logger.LogDebug("[GameLoop] measured loop rate: {Tps:F1} TPS, {Fps:F1} fps", measuredTps, measuredFps);
                    }

                    ticksThisWindow = 0;
                    framesThisWindow = 0;
                    rateWindow.Restart();
                }

                // ---- Wait for the next frame ----
                //
                // Pin each frame to an ABSOLUTE deadline rather than sleeping (target - work).
                //
                // Sleeping for the remainder only accounts for the work we measured; the time spent outside
                // that window, plus the sleep's own error, lands outside the calculation, so the period drifts
                // instead of being held. Measured on the old tick loop, that produced a steady 49ms against
                // the server's 50ms — about 2% fast, which sounds harmless but is not: we gained a whole tick
                // roughly every 50 ticks, and when two of our movement packets landed inside one server tick
                // the anticheat evaluated them as a single movement. One doubled packet became 800+ setbacks.
                //
                // The residual accumulator above means frame-timing error no longer changes the tick RATE —
                // an early or late frame just moves ticks between frames — but holding the frame period
                // steady still matters, because a drifting frame period produces a drifting emission pattern.
                // Frame times are NOT constant on a real client, and the difference is measurable in the tick
                // stream rather than cosmetic. With a perfectly regular 33.33 ms frame against a 50 ms tick,
                // the residual pattern is deterministic — exactly 33/67/33/67 — and a frame can never fall far
                // enough behind to owe two ticks. The vanilla control emitted 2244 same-millisecond tick pairs
                // in 27 minutes, which can only come from frames that ran long. Measured: a constant frame
                // gave stdev 17.14 ms against vanilla's 23.72; the variance below is what closes that gap.
                //
                // Shape: mostly small symmetric noise (scheduling, render load) with an occasional long frame
                // (GC, chunk mesh, disk). Deliberately part of the loop rather than the humanizer, because it
                // is vanilla parity, not evasion — the humanizer is switched off in the harness and locally,
                // which is exactly where we measure.
                var frameNoiseMs = (frameNoise.NextDouble() - 0.5) * (2.0 * FrameJitterMs);
                if (frameNoise.NextDouble() < FrameSpikeChance)
                {
                    frameNoiseMs += frameNoise.NextDouble() * FrameSpikeMs;
                }

                nextFrameDeadline += frameIntervalTicks + (long)(frameNoiseMs / 1000.0 * Stopwatch.Frequency);

                // Any configured humanizer jitter is folded into the target instant on top, so the precise
                // wait below knows exactly when to stop rather than sleeping for a duration and hoping.
                var jitterTicks = (long)(humanizer.GetTickJitterMs() / 1000.0 * Stopwatch.Frequency);
                var waitUntil = nextFrameDeadline + jitterTicks;

                var nowTimestamp = Stopwatch.GetTimestamp();
                var remainingMs = (waitUntil - nowTimestamp) * 1000.0 / Stopwatch.Frequency;

                if (remainingMs > 0.0)
                {
                    // Coarse sleep for the bulk, then spin the last stretch.
                    //
                    // Task.Delay resolves to the SYSTEM TIMER, which on Windows defaults to 15.6ms — half a
                    // frame. Sleeping the whole remainder therefore overshoots and the loop settles at the
                    // granularity rather than the target. The tail is spun rather than slept because no sleep
                    // is accurate enough to land it.
                    const double SpinTailMs = 16.0;

                    if (remainingMs > SpinTailMs)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(remainingMs - SpinTailMs), token);
                    }

                    while (Stopwatch.GetTimestamp() < waitUntil)
                    {
                        if (token.IsCancellationRequested) break;
                        Thread.SpinWait(60);
                    }
                }
                else
                {
                    // Only resynchronise when we are MORE THAN A FULL FRAME behind.
                    //
                    // Resetting the schedule on any overshoot at all is a one-way ratchet: a frame that ends
                    // 0.3 ms late pushes the deadline 0.3 ms later for ever, and nothing ever pulls it back,
                    // because an early frame just waits. Leaving the deadline alone for small overshoots lets
                    // the next wait come out shorter and absorb the overrun, which is what the absolute
                    // schedule is for.
                    if (nowTimestamp - nextFrameDeadline > frameIntervalTicks)
                    {
                        nextFrameDeadline = nowTimestamp;
                    }

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
