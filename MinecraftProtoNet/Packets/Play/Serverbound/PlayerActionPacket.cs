using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

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
        CancelDigging = 1,
        FinishedDigging = 2,
        DropItemStack = 3,
        DropItem = 4,
        ShootArrowOrFinishEating = 5,
        SwapItemWithOffhand = 6,
    }
}
