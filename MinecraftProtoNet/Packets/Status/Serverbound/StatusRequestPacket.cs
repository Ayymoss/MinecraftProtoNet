using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Status.Serverbound;

public class StatusRequestPacket : Packet
{
    public override int PacketId => 0x00;
    public override PacketDirection Direction => PacketDirection.Serverbound;

    public override void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(PacketId);
    }
}
