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
    void PhysicsTick();
    void SetPosition(int entityId, Vector3<double> newPosition, bool delta = true);
    MovePlayerPositionRotationPacket Move(double x, double y, double z, float yaw = 0, float pitch = 0);
}
