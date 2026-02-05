using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;

namespace MinecraftProtoNet.Core.Handlers.Base;

public interface IPacketHandler
{
    IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets { get; }
    Task HandleAsync(IClientboundPacket packet, IMinecraftClient client);
}
