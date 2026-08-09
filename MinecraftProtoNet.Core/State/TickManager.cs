using System.Diagnostics;
using System.Collections.Generic;
using MinecraftProtoNet.Core.Models.World;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// Manages tick timing and world time.
/// </summary>
public class TickManager : ITickManager
{
    /// <summary>Vanilla's fixed client tick target: 1000/20. Reference: Minecraft.java:284 Timer(20.0F, ...).</summary>
    public const double DefaultTickIntervalMs = 50d;

    /// <summary>
    /// The interval the client loop should use, mirroring vanilla's getTickTargetMillis + runsNormally():
    /// the server's rate applies only while running normally, is never faster than 50 ms, and is ignored
    /// entirely while frozen.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/Minecraft.java:2854
    /// </summary>
    public double TickInterval => IsFrozen
        ? DefaultTickIntervalMs
        : Math.Max(DefaultTickIntervalMs, _serverTickInterval);

    /// <summary>Interval implied by the server's ticking-state packet. Default = vanilla's 20 TPS.</summary>
    private double _serverTickInterval = DefaultTickIntervalMs;

    /// <summary>
    /// The server's real tick pace as measured from time packets. Diagnostic only: it says how laggy the
    /// server is, which is worth knowing but must not change our own rate.
    /// </summary>
    public double ObservedServerTickInterval { get; private set; } = 50d;
    public long ClientTickCounter { get; private set; }
    public long WorldAge { get; private set; }
    public long TimeOfDay { get; private set; }
    public bool TimeOfDayIncreasing { get; private set; }
    public Stopwatch TimeSinceLastTimePacket { get; } = new();
    
    private readonly Lock _tickLock = new();

    public void UpdateTickInformation(long serverWorldAge, long timeOfDay, bool timeOfDayIncreasing)
    {
        lock (_tickLock)
        {
            var previousServerWorldAge = WorldAge;

            WorldAge = serverWorldAge;
            TimeOfDay = timeOfDay;
            TimeOfDayIncreasing = timeOfDayIncreasing;

            if (previousServerWorldAge > 0)
            {
                var serverTicksPassed = WorldAge - previousServerWorldAge;
                if (serverTicksPassed > 0)
                {
                    var realTimeElapsed = TimeSinceLastTimePacket.ElapsedMilliseconds;
                    TimeSinceLastTimePacket.Restart();

                    if (realTimeElapsed > 0)
                    {
                        // OBSERVED only — this must not drive our own tick rate.
                        //
                        // It used to feed straight into TickInterval, so the client loop tracked whatever rate
                        // the server was actually managing. On a busy Hypixel hub that runs below 20 TPS, we
                        // slowed down with it: measured mean tick 51.1-51.6 ms (19.4-19.6 TPS) against a real
                        // client's exactly 50.0 ms in two independent captures.
                        //
                        // A vanilla client never does this. Its loop is a fixed 50 ms and only changes when the
                        // server explicitly says so via the ticking-state packet (TickRateManager / SetTickRate
                        // below). Server lag changes how fast the WORLD advances, not how fast the client ticks
                        // — so following it made us send measurably fewer packets per second than any real
                        // player on the same laggy server.
                        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/TickRateManager.java
                        var calculatedTickInterval = (double)realTimeElapsed / serverTicksPassed;
                        const double smoothingFactor = 0.25;
                        ObservedServerTickInterval = Math.Clamp(
                            ObservedServerTickInterval * (1 - smoothingFactor) + calculatedTickInterval * smoothingFactor,
                            5.0, 1000.0);
                    }
                }
                else
                {
                    TimeSinceLastTimePacket.Restart();
                }
            }
            else
            {
                TimeSinceLastTimePacket.Restart();
            }

            // ClientTickCounter is deliberately NOT assigned here.
            //
            // It used to be `ClientTickCounter = serverWorldAge`, which made the "client tick counter" a
            // resampled copy of the server's world age. Vanilla keeps the two completely separate:
            //   Minecraft.java:373  private long clientTickCount;   <- written ONLY by ++ in tick() (:1722)
            //   ClientPacketListener.java:1036  level.setTimeFromServer(gameTime)  <- server time lives on Level
            // and ClientLevel advances its own copy locally each tick (ClientLevel.java:401-405).
            //
            // Resampling made our counter non-monotonic: a proxy backend switch (routine on Hypixel) moves the
            // world age by an arbitrary amount in EITHER direction. Everything that differenced tick numbers
            // broke across that discontinuity — block-break progress, right-click delay, per-tick caches, and
            // every "is the loop alive?" probe, which a SetTimePacket alone could satisfy while the game loop
            // was wedged.
        }
    }

    public void UpdateTickInformation(long gameTime, Dictionary<int, ClockState> clockUpdates)
    {
        // For legacy compatibility, we try to pick the "main" clock (ID 0 usually overworld)
        // and map it to TimeOfDay / TimeOfDayIncreasing.
        ClockState? targetState = null;
        if (clockUpdates.TryGetValue(0, out var overworldState))
        {
            targetState = overworldState;
        }
        else if (clockUpdates.Values.FirstOrDefault() is { } firstState)
        {
            targetState = firstState;
        }

        if (targetState is not null)
        {
            UpdateTickInformation(gameTime, targetState.TotalTicks, !targetState.Paused);
        }
        else
        {
            // If no clocks are provided, we only update world age (GameTime)
            UpdateTickInformation(gameTime, TimeOfDay, TimeOfDayIncreasing);
        }
    }

    public void IncrementClientTickCounter()
    {
        lock (_tickLock)
        {
            ClientTickCounter++;
        }
    }

    /// <summary>
    /// The SERVER's measured tick rate, from time packets — not our own loop rate.
    ///
    /// This used to divide by TickInterval, which was the same number back when our loop chased the server.
    /// Now that the loop is pinned at 50 ms it would have reported a flat 20 TPS however badly the server was
    /// lagging, which is exactly backwards for a diagnostic whose whole purpose is to reveal server lag.
    /// </summary>
    public double GetCurrentServerTps()
    {
        const double epsilon = 1e-9;
        return ObservedServerTickInterval > epsilon ? 1000.0 / ObservedServerTickInterval : 0.0;
    }

    public bool IsFrozen { get; private set; }

    public void SetTickRate(float tickRate)
    {
        lock (_tickLock)
        {
            tickRate = Math.Max(tickRate, 1.0f);

            // max(50 ms, server rate) — vanilla can be slowed by the server but NEVER sped up past 20 TPS.
            // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/Minecraft.java:2854
            //   getTickTargetMillis -> Math.max(defaultTickTargetMillis, manager.millisecondsPerTick())
            // with default 50 ms from `new DeltaTracker.Timer(20.0F, ...)`.
            // Without the clamp a server advertising >20 TPS would make us tick faster than any real client,
            // and ticking fast is the direction that previously produced 800+ setbacks here.
            _serverTickInterval = 1000.0 / tickRate;
        }
    }

    public void SetFrozen(bool frozen)
    {
        lock (_tickLock)
        {
            IsFrozen = frozen;
        }
    }
}
