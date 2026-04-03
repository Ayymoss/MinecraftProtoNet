using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Stops a sound or all sounds on the client.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundStopSoundPacket.java
/// </summary>
[Packet(0x77, ProtocolState.Play, silent: true)]
public class StopSoundPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // Flags byte + optional source enum + optional resource location — not needed by the bot
    }
}
