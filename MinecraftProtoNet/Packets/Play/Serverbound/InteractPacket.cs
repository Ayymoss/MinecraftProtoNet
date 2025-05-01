using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x18, ProtocolState.Play)]
public class InteractPacket : IServerboundPacket
{
    public required int EntityId { get; set; }
    public required InteractType Type { get; set; }
    public float? TargetX { get; set; }
    public float? TargetY { get; set; }
    public float? TargetZ { get; set; }
    public Hand? Hand { get; set; }
    public required bool SneakKeyPressed { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(EntityId);
        buffer.WriteVarInt((byte)Type);

        if (Type == InteractType.InteractAt)
        {
            if (TargetX is null || TargetY is null || TargetZ is null)
            {
                throw new ArgumentNullException(nameof(TargetX), "Target coordinates cannot be null for InteractAt type.");
            }

            buffer.WriteFloat(TargetX.Value);
            buffer.WriteFloat(TargetY.Value);
            buffer.WriteFloat(TargetZ.Value);
        }

        if (Type is InteractType.Interact or InteractType.InteractAt)
        {
            if (Hand is null)
            {
                throw new ArgumentNullException(nameof(Hand), " Hand cannot be null for Interact or InteractAt type.");
            }

            buffer.WriteVarInt((int)Hand);
        }

        buffer.WriteBoolean(SneakKeyPressed);
    }
}
