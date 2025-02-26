using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Serverbound;

public class LoginAcknowledgedPacket : Packet
{
    public override int PacketId => 0x03;
    public override PacketDirection Direction => PacketDirection.Serverbound;
}
