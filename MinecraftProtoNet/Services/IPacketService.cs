using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Services;

public interface IPacketService
{
    Task HandlePacketAsync(Packet packet, IMinecraftClient client);
    Packet CreateIncomingPacket(ProtocolState state, int packetId);
}
