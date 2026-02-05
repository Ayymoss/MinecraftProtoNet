using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x1D, ProtocolState.Play)]
public class MovePlayerPositionPacket : IServerboundPacket
{
    public required double X { get; set; }
    public required double Y { get; set; }
    public required double Z { get; set; }
    public required MovementFlags Flags { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteDouble(X);
        buffer.WriteDouble(Y);
        buffer.WriteDouble(Z);
        buffer.WriteUnsignedByte((byte)Flags);
    }
}
