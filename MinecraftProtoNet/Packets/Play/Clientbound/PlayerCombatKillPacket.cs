using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

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
