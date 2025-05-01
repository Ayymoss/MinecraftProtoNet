using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Services;

public interface IPacketService
{
    Task HandlePacketAsync(IClientboundPacket packet, IMinecraftClient client);
    IClientboundPacket CreateIncomingPacket(ProtocolState state, int packetId);
}
