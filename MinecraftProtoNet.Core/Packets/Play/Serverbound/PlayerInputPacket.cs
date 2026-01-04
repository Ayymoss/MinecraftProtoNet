using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x2A, ProtocolState.Play)]
public class PlayerInputPacket(PlayerInputPacket.MovementFlag flag) : IServerboundPacket
{
    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteUnsignedByte((byte)flag);
    }

    public enum MovementFlag : byte
    {
        Forward = 0x01,
        Backward = 0x02,
        Left = 0x04,
        Right = 0x08,
        Jump = 0x10,
        Sneak = 0x20,
        Sprint = 0x40,
    }
}
