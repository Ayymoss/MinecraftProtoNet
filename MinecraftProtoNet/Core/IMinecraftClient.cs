using MinecraftProtoNet.Auth.Dtos;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.State.Base;

namespace MinecraftProtoNet.Core;

public interface IMinecraftClient
{
    ProtocolState ProtocolState { get; set; }
    ClientState State { get; }
    int ProtocolVersion { get; set; }
    AuthResult AuthResult { get; set; }
    
    
    /// <summary>
    /// Raised when the client disconnects from the server.
    /// </summary>
    event EventHandler<DisconnectReason>? OnDisconnected;
    
    Task<bool> AuthenticateAsync();
    void EnableEncryption(byte[] sharedSecret);
    void EnableCompression(int threshold);
    Task ConnectAsync(string host, int port, bool isSnapshot = false);
    Task DisconnectAsync();
    Task SendPacketAsync(IServerboundPacket packet);

    Task HandleChatMessageAsync(Guid senderGuid, string bodyMessage);

    Task PhysicsTickAsync();
    Task SendChatSessionUpdate();

    /// <summary>
    /// Gets the pathfinding service for navigation.
    /// </summary>
    Pathfinding.IPathingService PathingService { get; }
}

