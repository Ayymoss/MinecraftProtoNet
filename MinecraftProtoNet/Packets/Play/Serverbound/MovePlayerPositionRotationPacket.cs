using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x1D, ProtocolState.Play)]
public class MovePlayerPositionRotationPacket : IServerPacket
{
    public required MovePlayerPositionRotation Payload { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(this.GetPacketAttributeValue(p => p.PacketId));

        buffer.WriteDouble(Payload.X);
        buffer.WriteDouble(Payload.Y);
        buffer.WriteDouble(Payload.Z);
        buffer.WriteFloat(Payload.Yaw);
        buffer.WriteFloat(Payload.Pitch);
        buffer.WriteSignedByte((byte)Payload.MovementFlags);
    }

    public class MovePlayerPositionRotation
    {
        public required double X { get; set; }
        public required double Y { get; set; }
        public required double Z { get; set; }
        public required float Yaw { get; set; }
        public required float Pitch { get; set; }
        public required MovementFlags MovementFlags { get; set; }
    }

    public enum MovementFlags
    {
        None,
        OnGround,
        PushingAgainstWall
    }
}
