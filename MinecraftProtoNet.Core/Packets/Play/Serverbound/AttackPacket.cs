using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

/// <summary>
/// Left-click (attack) on an entity. Split out of the Interact packet in 26.x, and now carries nothing but the
/// target: the hand comes from the held slot and the swing is a separate packet.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/game/ServerboundAttackPacket.java
/// </summary>
[Packet(0x01, ProtocolState.Play)]
public class AttackPacket : IServerboundPacket
{
    public required int EntityId { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(EntityId);
    }
}
