using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x3C, ProtocolState.Play)]
public class SwingPacket : IServerboundPacket
{
    public Hand Hand { get; set; } = Hand.MainHand;

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt((int)Hand);
    }
}
