using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Serverbound;

public class FinishConfigurationPacket : Packet
{
    public override int PacketId => 0x03;
    public override PacketDirection Direction => PacketDirection.Serverbound;
    public override void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(PacketId);
    }
}
