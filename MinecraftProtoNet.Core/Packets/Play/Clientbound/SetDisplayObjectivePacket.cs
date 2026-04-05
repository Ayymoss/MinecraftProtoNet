using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Sets which objective is displayed in a particular scoreboard position.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetDisplayObjectivePacket.java
/// </summary>
[Packet(0x62, ProtocolState.Play, silent: false)]
public class SetDisplayObjectivePacket : IClientboundPacket
{
    public int Position { get; set; }
    public string ObjectiveName { get; set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Position = buffer.ReadVarInt();
        ObjectiveName = buffer.ReadString();
    }
}
