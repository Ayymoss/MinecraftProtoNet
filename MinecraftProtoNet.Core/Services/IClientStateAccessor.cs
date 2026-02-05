using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Provides access to the current client state for dependency injection.
/// Allows services to access Level and LocalPlayer without passing them explicitly.
/// </summary>
public interface IClientStateAccessor
{
    /// <summary>
    /// Gets the current Level. Always returns the current value (lazy access).
    /// </summary>
    Level Level { get; }

    /// <summary>
    /// Gets the local player's entity. Always returns the current value (lazy access).
    /// </summary>
    Entity? LocalPlayer { get; }

    /// <summary>
    /// Gets the full ClientState for advanced access.
    /// </summary>
    ClientState State { get; }
}
