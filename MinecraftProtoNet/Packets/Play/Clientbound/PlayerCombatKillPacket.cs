using MinecraftProtoNet.Core;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class PlayerCombatKillPacket : Packet
{
    public override int PacketId => 0x3E;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public int PlayerId { get; set; }
    public NbtTag DeathMessage { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        PlayerId = buffer.ReadVarInt();
        DeathMessage = buffer.ReadNbtTag();
    }
}
