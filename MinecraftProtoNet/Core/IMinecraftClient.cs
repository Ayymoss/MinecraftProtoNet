using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.State.Base;

namespace MinecraftProtoNet.Core;

public interface IMinecraftClient
{
    ProtocolState State { get; set; }
    int ProtocolVersion { get; set; }
    ClientState ClientState { get; }
    Task ConnectAsync(string host, int port);
    Task DisconnectAsync();
    Task SendPacketAsync(IServerPacket packet);
}
