using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class PlayerChatPacket : Packet
{
    public override int PacketId { get; }
    public override PacketDirection Direction { get; }
}
