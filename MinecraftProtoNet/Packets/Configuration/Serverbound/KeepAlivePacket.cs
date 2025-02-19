using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Serverbound;

public class KeepAlivePacket : Packet
{
    public override int PacketId => 0x04;
    public override PacketDirection Direction => PacketDirection.Serverbound;

    public required long Payload { get; set; }

    public override void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(PacketId);
        buffer.WriteSignedLong(Payload);
    }
}
