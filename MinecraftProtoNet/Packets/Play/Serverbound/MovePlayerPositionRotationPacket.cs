using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

public class MovePlayerPositionRotationPacket : Packet
{
    public override int PacketId => 0x1D;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public required MovePlayerPositionRotation Payload { get; set; }

    public override void Serialize(ref PacketBufferWriter buffer)
    {
        base.Serialize(ref buffer);

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
