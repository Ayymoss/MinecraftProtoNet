using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

/// <summary>
/// Sent when only ground/collision status flags change (no position or rotation change).
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ServerboundMovePlayerPacket.java:194-215
/// </summary>
[Packet(0x21, ProtocolState.Play)]
public class MovePlayerStatusOnlyPacket : IServerboundPacket
{
    public required MovementFlags Flags { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteUnsignedByte((byte)Flags);
    }
}
