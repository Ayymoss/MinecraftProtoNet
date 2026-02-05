using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Core.Core.Abstractions;

/// <summary>
/// Interface for the main game loop that drives physics ticks.
/// </summary>
public interface IGameLoop
{
    /// <summary>
    /// Whether the game loop is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the game loop.
    /// </summary>
    void Start(IMinecraftClient client);

    /// <summary>
    /// Event fired on each physics tick for an entity.
    /// </summary>
    event Action<Entity>? PhysicsTick;

    /// <summary>
    /// Event fired before each physics tick. External systems (e.g., Baritone) can subscribe to this.
    /// </summary>
    event Action<IMinecraftClient>? PreTick;

    /// <summary>
    /// Event fired after each physics tick. External systems (e.g., Baritone) can subscribe to this.
    /// </summary>
    event Action<IMinecraftClient>? PostTick;

    /// <summary>
    /// Stops the game loop.
    /// </summary>
    Task StopAsync();
}
