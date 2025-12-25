using MinecraftProtoNet.Core;

namespace MinecraftProtoNet.Core.Abstractions;

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
    /// Stops the game loop.
    /// </summary>
    Task StopAsync();
}
