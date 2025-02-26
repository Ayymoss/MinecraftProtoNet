using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

public class KeepAlivePacket : Packet
{
    public override int PacketId => 0x1A;
    public override PacketDirection Direction => PacketDirection.Serverbound;

    public required long Payload { get; set; }

    public override void Serialize(ref PacketBufferWriter buffer)
    {
        base.Serialize(ref buffer);

        buffer.WriteSignedLong(Payload);
    }
}
