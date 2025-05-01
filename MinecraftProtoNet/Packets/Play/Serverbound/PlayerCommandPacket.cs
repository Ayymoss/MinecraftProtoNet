using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x28, ProtocolState.Play)]
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
