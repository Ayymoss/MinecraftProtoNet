using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Sets the header and footer of the player list (tab list).
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundTabListPacket.java
/// </summary>
[Packet(0x7A, ProtocolState.Play, silent: true)]
public class TabListPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // Two Components (header + footer) — variable length, not needed by the bot
    }
}
