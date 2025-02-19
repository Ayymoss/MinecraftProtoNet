using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

public class FinishConfigurationPacket : Packet
{
    public override int PacketId => 0x03;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        // No fields to deserialize
    }
}
