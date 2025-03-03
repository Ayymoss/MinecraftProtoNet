using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Services;

public interface IPacketService
{
    Task HandlePacketAsync(IClientPacket packet, IMinecraftClient client);
    IClientPacket CreateIncomingPacket(ProtocolState state, int packetId);
}
