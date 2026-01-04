using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x28, ProtocolState.Play)]
public class PlayerActionPacket : IServerboundPacket
{
    public required StatusType Status { get; set; }
    public required Vector3<double> Position { get; set; }
    public required BlockFace Face { get; set; }
    public required int Sequence { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt((int)Status);
        buffer.WritePosition(Position);
        buffer.WriteUnsignedByte((byte)Face);
        buffer.WriteVarInt(Sequence);
    }

    public enum StatusType
    {
        StartedDigging = 0,
        CancelledDigging = 1,
        FinishedDigging = 2,
        DropItemStack = 3,
        DropItem = 4,
        ShootArrowOrFinishEating = 5,
        SwapItemWithOffhand = 6,
    }
}
