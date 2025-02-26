using MinecraftProtoNet.Core;

namespace MinecraftProtoNet.Packets.Base;

public class UnknownPacket : Packet
{
    public override int PacketId => -0x01;
    public override PacketDirection Direction => PacketDirection.Clientbound;
}
