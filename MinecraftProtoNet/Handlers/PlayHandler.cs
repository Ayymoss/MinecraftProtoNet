using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Handlers;

public class PlayHandler : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets => [];
    public Task HandleAsync(Packet packet, IMinecraftClient client)
    {
        throw new NotImplementedException();
    }
}
