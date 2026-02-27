using System.Diagnostics;
using System.Collections.Generic;
using MinecraftProtoNet.Core.Models.World;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// Manages tick timing and world time.
/// </summary>
public class TickManager : ITickManager
{
    public double TickInterval { get; private set; } = 50d;
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
                        var calculatedTickInterval = (double)realTimeElapsed / serverTicksPassed;
                        const double smoothingFactor = 0.25;
                        TickInterval = TickInterval * (1 - smoothingFactor) + calculatedTickInterval * smoothingFactor;
                        TickInterval = Math.Clamp(TickInterval, 5.0, 1000.0);
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

            ClientTickCounter = serverWorldAge;
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

    public double GetCurrentServerTps()
    {
        const double epsilon = 1e-9;
        return TickInterval > epsilon ? 1000.0 / TickInterval : 0.0;
    }

    public bool IsFrozen { get; private set; }

    public void SetTickRate(float tickRate)
    {
        lock (_tickLock)
        {
            tickRate = Math.Max(tickRate, 1.0f);
            TickInterval = 1000.0 / tickRate;
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
