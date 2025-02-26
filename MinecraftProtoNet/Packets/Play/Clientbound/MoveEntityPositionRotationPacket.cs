using System.Numerics;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class MoveEntityPositionRotationPacket : Packet
{
    public override int PacketId => 0x30;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public EntityPositionAndRotation Payload { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = new EntityPositionAndRotation
        {
            EntityId = buffer.ReadVarInt(),
            DeltaX = buffer.ReadSignedShort(),
            DeltaY = buffer.ReadSignedShort(),
            DeltaZ = buffer.ReadSignedShort(),
            Yaw = new Vector<byte>(buffer.ReadUnsignedByte()),
            Pitch = new Vector<byte>(buffer.ReadUnsignedByte()),
            OnGround = buffer.ReadBoolean()
        };
    }

    public class EntityPositionAndRotation
    {
        public required int EntityId { get; set; }
        public required short DeltaX { get; set; }
        public required short DeltaY { get; set; }
        public required short DeltaZ { get; set; }
        public required Vector<byte> Yaw { get; set; }
        public required Vector<byte> Pitch { get; set; }
        public required bool OnGround { get; set; }
    }
}
