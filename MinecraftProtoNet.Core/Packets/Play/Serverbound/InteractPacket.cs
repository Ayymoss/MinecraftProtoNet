using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

/// <summary>
/// Right-click on an entity.
///
/// 26.x reshaped this packet: the old action enum (INTERACT / ATTACK / INTERACT_AT) is gone. Attacking is now
/// its own packet (<see cref="AttackPacket"/>), and the hit location is always present, carried in the packed
/// low-precision Vec3 format rather than three floats. Sending the pre-26 layout makes the server read the
/// action byte as the hand, which throws in its decoder and drops the connection — on Hypixel that surfaces as
/// "A disconnect occurred in your connection, so you were put in the SkyBlock Lobby!".
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/game/ServerboundInteractPacket.java
/// </summary>
[Packet(0x1A, ProtocolState.Play)]
public class InteractPacket : IServerboundPacket
{
    public required int EntityId { get; set; }

    public Hand Hand { get; set; } = Hand.MainHand;

    /// <summary>
    /// Where on the entity the cursor landed, RELATIVE to the entity's position — vanilla passes
    /// <c>hitResult.getLocation().subtract(entity.position())</c>.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/multiplayer/MultiPlayerGameMode.java
    /// </summary>
    public Vector3<double> Location { get; set; } = new(0, 0, 0);

    /// <summary>Sneaking at the time of the click; changes what some interactions do.</summary>
    public required bool SneakKeyPressed { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(EntityId);
        buffer.WriteVarInt((int)Hand);
        LpVec3.Write(ref buffer, Location);
        buffer.WriteBoolean(SneakKeyPressed);
    }
}
