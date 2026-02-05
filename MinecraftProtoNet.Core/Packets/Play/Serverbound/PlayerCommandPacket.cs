using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x29, ProtocolState.Play)]
public class PlayerCommandPacket : IServerboundPacket
{
    public required int EntityId { get; set; }
    public required PlayerAction Action { get; set; }
    public int JumpBoost { get; set; } = 0; // Only used by StartJumpWithHorse.

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(EntityId);
        buffer.WriteVarInt((int)Action);
        buffer.WriteVarInt(JumpBoost);
    }
}
