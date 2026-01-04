using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x00, ProtocolState.Play, true)]
public class BundleDelimiterPacket : IClientboundPacket
{
    // TODO: The implication for this packet needs to be implemented.
    public void Deserialize(ref PacketBufferReader buffer)
    {
    }
}
