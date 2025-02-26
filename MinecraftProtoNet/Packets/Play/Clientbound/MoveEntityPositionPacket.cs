using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class MoveEntityPositionPacket : Packet
{
    public override int PacketId => 0x2F;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public EntityPosition Payload { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = new EntityPosition
        {
            EntityId = buffer.ReadVarInt(),
            DeltaX = buffer.ReadSignedShort(),
            DeltaY = buffer.ReadSignedShort(),
            DeltaZ = buffer.ReadSignedShort(),
            OnGround = buffer.ReadBoolean()
        };
    }

    public class EntityPosition
    {
        public required int EntityId { get; set; }
        public required short DeltaX { get; set; }
        public required short DeltaY { get; set; }
        public required short DeltaZ { get; set; }
        public required bool OnGround { get; set; }
    }
}
