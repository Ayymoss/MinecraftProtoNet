using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Status.Serverbound;

public class StatusRequestPacket : Packet
{
    public override int PacketId => 0x00;
    public override PacketDirection Direction => PacketDirection.Serverbound;
}
