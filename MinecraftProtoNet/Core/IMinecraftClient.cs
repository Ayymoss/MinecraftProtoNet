using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Core;

public interface IMinecraftClient
{
    ProtocolState State { get; set; }
    int ProtocolVersion { get; set; }
    Task ConnectAsync(string host, int port);
    Task DisconnectAsync();
    Task SendPacketAsync(IOutgoingPacket packet);
}
