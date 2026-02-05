using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x3C, ProtocolState.Play)]
public class SwingPacket : IServerboundPacket
{
    public Hand Hand { get; set; } = Hand.MainHand;

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt((int)Hand);
    }
}
