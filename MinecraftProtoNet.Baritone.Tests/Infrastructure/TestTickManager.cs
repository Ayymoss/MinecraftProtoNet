using System.Diagnostics;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Baritone.Tests.Infrastructure;

/// <summary>
/// Test implementation of ITickManager for unit tests.
/// </summary>
public class TestTickManager : ITickManager
{
    public double TickInterval { get; set; } = 0.05; // 20 TPS default
    public long ClientTickCounter { get; set; }
    public long WorldAge { get; set; }
    public long TimeOfDay { get; set; }
    public bool TimeOfDayIncreasing { get; set; } = true;
    public Stopwatch TimeSinceLastTimePacket { get; } = Stopwatch.StartNew();

    public void UpdateTickInformation(long serverWorldAge, long timeOfDay, bool timeOfDayIncreasing)
    {
        WorldAge = serverWorldAge;
        TimeOfDay = timeOfDay;
        TimeOfDayIncreasing = timeOfDayIncreasing;
        TimeSinceLastTimePacket.Restart();
    }

    public void IncrementClientTickCounter() => ClientTickCounter++;

    public double GetCurrentServerTps() => 20.0;
}
