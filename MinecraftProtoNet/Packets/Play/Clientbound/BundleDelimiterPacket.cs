using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x00, ProtocolState.Play, true)]
public class BundleDelimiterPacket : IClientboundPacket
{
    // TODO: The implication for this packet needs to be implemented.
    public void Deserialize(ref PacketBufferReader buffer)
    {
    }
}
