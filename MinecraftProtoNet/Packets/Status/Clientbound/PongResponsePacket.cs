using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Status.Clientbound;

public class PongResponsePacket : Packet
{
    public override int PacketId => 0x01;
    public override PacketDirection Direction => PacketDirection.Clientbound;
    public long Payload { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = buffer.ReadSignedLong();
    }
}
