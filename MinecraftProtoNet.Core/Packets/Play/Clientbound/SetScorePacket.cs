using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Updates a score on a scoreboard objective.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetScorePacket.java
/// </summary>
[Packet(0x6E, ProtocolState.Play, silent: false)]
public class SetScorePacket : IClientboundPacket
{
    public string Owner { get; set; } = string.Empty;
    public string ObjectiveName { get; set; } = string.Empty;
    public int Value { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Owner = buffer.ReadString();
        ObjectiveName = buffer.ReadString();
        Value = buffer.ReadVarInt();
        // Optional display name (chat component) + optional number format
        // Consume rest since we don't need scoreboard data.
        buffer.ReadRestBuffer();
    }
}
