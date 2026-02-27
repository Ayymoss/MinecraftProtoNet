using System.Diagnostics;
using System.Collections.Generic;
using MinecraftProtoNet.Core.Models.World;

namespace MinecraftProtoNet.Core.State;

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
    /// Updates tick information from a server time packet (legacy format).
    /// </summary>
    void UpdateTickInformation(long serverWorldAge, long timeOfDay, bool timeOfDayIncreasing);

    /// <summary>
    /// Updates tick information from a server time packet (new snapshot format).
    /// </summary>
    void UpdateTickInformation(long gameTime, Dictionary<int, ClockState> clockUpdates);

    /// <summary>
    /// Increments the client tick counter.
    /// </summary>
    void IncrementClientTickCounter();

    /// <summary>
    /// Gets the current server TPS (ticks per second).
    /// </summary>
    double GetCurrentServerTps();

    /// <summary>
    /// Whether the game is frozen (ticking paused by server).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/TickRateManager.java
    /// </summary>
    bool IsFrozen { get; }

    /// <summary>
    /// Sets the tick rate from a server TickingState packet.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/TickRateManager.java:22-25
    /// </summary>
    void SetTickRate(float tickRate);

    /// <summary>
    /// Sets the frozen state from a server TickingState packet.
    /// </summary>
    void SetFrozen(bool frozen);
}
