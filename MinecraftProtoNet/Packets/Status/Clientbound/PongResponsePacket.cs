using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Status.Clientbound;

[Packet(0x01, ProtocolState.Status)]
public class PongResponsePacket : IClientboundPacket
{
    public long Payload { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = buffer.ReadSignedLong();
    }
}
