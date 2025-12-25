using System.Diagnostics;

namespace MinecraftProtoNet.State;

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
}
