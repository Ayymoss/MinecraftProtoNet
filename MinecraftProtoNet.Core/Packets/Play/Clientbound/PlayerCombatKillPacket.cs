using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x43, ProtocolState.Play)]
public class PlayerCombatKillPacket : IClientboundPacket
{
    public int PlayerId { get; set; }
    public required NbtTag DeathMessage { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        PlayerId = buffer.ReadVarInt();
        DeathMessage = buffer.ReadNbtTag();
    }
}
