using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Sets the title text displayed on the client.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetTitleTextPacket.java
/// </summary>
[Packet(0x72, ProtocolState.Play, silent: true)]
public class SetTitleTextPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // Component (JSON chat) — variable length, not needed by the bot
    }
}
