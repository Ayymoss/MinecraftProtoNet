using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x19, ProtocolState.Play)]
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
