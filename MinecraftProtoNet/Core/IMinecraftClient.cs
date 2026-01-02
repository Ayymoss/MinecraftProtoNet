using MinecraftProtoNet.Auth.Dtos;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.State.Base;
using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Core;

public interface IMinecraftClient : IPacketSender
{
    ProtocolState ProtocolState { get; set; }
    ClientState State { get; }
    int ProtocolVersion { get; set; }
    AuthResult? AuthResult { get; set; }
    
    /// <summary>
    /// Whether the client is currently connected to a server.
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Gets the interaction manager.
    /// </summary>
    IInteractionManager InteractionManager { get; }

    /// <summary>
    /// Raised when the client disconnects from the server.
    /// </summary>
    event EventHandler<DisconnectReason>? OnDisconnected;
    
    Task<bool> AuthenticateAsync();
    void EnableEncryption(byte[] sharedSecret);
    void EnableCompression(int threshold);
    Task ConnectAsync(string host, int port, bool isSnapshot = false);
    Task DisconnectAsync();

    Task HandleChatMessageAsync(Guid senderGuid, string bodyMessage);

    /// <summary>
    /// Performs a physics tick for the local player.
    /// </summary>
    /// <param name="prePhysicsCallback">Optional callback for pathfinding or AI logic</param>
    Task PhysicsTickAsync(Action<State.Entity>? prePhysicsCallback = null);

    Task SendChatSessionUpdate();
}

