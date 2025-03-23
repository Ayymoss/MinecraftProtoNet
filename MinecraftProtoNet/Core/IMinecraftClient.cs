using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.State.Base;

namespace MinecraftProtoNet.Core;

public interface IMinecraftClient
{
    ProtocolState ProtocolState { get; set; }
    ClientState State { get; }
    int ProtocolVersion { get; set; }
    Task ConnectAsync(string host, int port);
    Task DisconnectAsync();
    Task SendPacketAsync(IServerPacket packet);

    // TODO: Move these
    Task HandleChatMessageAsync(Guid senderGuid, string bodyMessage);
    Task PhysicsTickAsync();
    MovePlayerPositionRotationPacket Move(double x, double y, double z, float yaw, float pitch);
}
