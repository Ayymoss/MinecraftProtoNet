using System.Diagnostics;

namespace MinecraftProtoNet.State;

/// <summary>
/// Interface for managing tick timing and world time.
/// </summary>
public interface ITickManager
{
    /// <summary>
    /// The current tick interval in milliseconds (smoothed).
    /// </summary>
    double TickInterval { get; }

    /// <summary>
    /// The client's local tick counter.
    /// </summary>
    long ClientTickCounter { get; }

    /// <summary>
    /// The server's world age in ticks.
    /// </summary>
    long WorldAge { get; }

    /// <summary>
    /// The current time of day in ticks.
    /// </summary>
    long TimeOfDay { get; }

    /// <summary>
    /// Whether the time of day is increasing.
    /// </summary>
    bool TimeOfDayIncreasing { get; }

    /// <summary>
    /// Stopwatch tracking time since the last time packet.
    /// </summary>
    Stopwatch TimeSinceLastTimePacket { get; }

    /// <summary>
    /// Updates tick information from a server time packet.
    /// </summary>
    void UpdateTickInformation(long serverWorldAge, long timeOfDay, bool timeOfDayIncreasing);

    /// <summary>
    /// Increments the client tick counter.
    /// </summary>
    void IncrementClientTickCounter();

    /// <summary>
    /// Gets the current server TPS (ticks per second).
    /// </summary>
    double GetCurrentServerTps();
}
