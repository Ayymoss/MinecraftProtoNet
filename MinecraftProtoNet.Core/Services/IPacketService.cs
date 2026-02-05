using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;

namespace MinecraftProtoNet.Core.Services;

public interface IPacketService
{
    Task HandlePacketAsync(IClientboundPacket packet, IMinecraftClient client);
    IClientboundPacket CreateIncomingPacket(ProtocolState state, int packetId);
}
