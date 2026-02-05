using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Provides access to the current client state via DI.
/// Directly injects ClientState singleton to avoid circular dependencies.
/// </summary>
public class ClientStateAccessor : IClientStateAccessor
{
    private readonly ClientState _state;

    public ClientStateAccessor(ClientState state)
    {
        _state = state;
    }

    /// <inheritdoc/>
    public Level Level => _state.Level;

    /// <inheritdoc/>
    public Entity? LocalPlayer => _state.LocalPlayer.Entity;

    /// <inheritdoc/>
    public ClientState State => _state;
}
