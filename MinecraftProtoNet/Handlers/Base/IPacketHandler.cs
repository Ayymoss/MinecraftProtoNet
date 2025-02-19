using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Handlers.Base;

public interface IPacketHandler
{
    IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets { get; }
    Task HandleAsync(Packet packet, IMinecraftClient client);
}
