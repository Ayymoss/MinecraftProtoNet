using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Sets a single slot in the player's inventory (not a container).
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetPlayerInventoryPacket.java
/// </summary>
[Packet(0x6C, ProtocolState.Play, silent: true)]
public class SetPlayerInventoryPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // VarInt slot + ItemStack contents — not needed by the bot (ContainerSetSlot covers inventory sync)
    }
}
