using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x1E, ProtocolState.Play, true)]
public class MovePlayerPositionRotationPacket : IServerboundPacket
{
    public required double X { get; set; }
    public required double Y { get; set; }
    public required double Z { get; set; }
    public required float Yaw { get; set; }
    public required float Pitch { get; set; }
    public required MovementFlags Flags { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteDouble(X);
        buffer.WriteDouble(Y);
        buffer.WriteDouble(Z);
        buffer.WriteFloat(Yaw);
        buffer.WriteFloat(Pitch);
        buffer.WriteUnsignedByte((byte)Flags);
    }
}
