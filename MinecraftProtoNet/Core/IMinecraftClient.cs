using MinecraftProtoNet.Auth.Dtos;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.State.Base;

namespace MinecraftProtoNet.Core;

public interface IMinecraftClient
{
    ProtocolState ProtocolState { get; set; }
    ClientState State { get; }
    int ProtocolVersion { get; set; }
    AuthResult AuthResult { get; set; }
    Task<bool> AuthenticateAsync();
    void EnableEncryption(byte[] sharedSecret);
    void EnableCompression(int threshold);
    Task ConnectAsync(string host, int port);
    Task DisconnectAsync();
    Task SendPacketAsync(IServerboundPacket packet);

    Task HandleChatMessageAsync(Guid senderGuid, string bodyMessage);
}
