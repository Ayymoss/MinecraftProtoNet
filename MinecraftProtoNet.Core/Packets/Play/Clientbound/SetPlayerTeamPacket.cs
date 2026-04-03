using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Creates, removes, or updates a scoreboard team.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetPlayerTeamPacket.java
/// </summary>
[Packet(0x6D, ProtocolState.Play, silent: true)]
public class SetPlayerTeamPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // Complex variable-length packet (team name, method, optional parameters, optional player list)
        // Not needed by the bot
    }
}
