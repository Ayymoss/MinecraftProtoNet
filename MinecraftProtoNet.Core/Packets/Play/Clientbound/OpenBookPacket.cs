using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Server tells the client to open the written book in the given hand.
///
/// Nothing in normal Bazaar work causes this, which is exactly why it is worth noticing: a server that wants a
/// human to read something — a warning, or a challenge — has few ways to force it in front of them, and this
/// is one.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundOpenBookPacket.java
/// </summary>
[Packet(0x3A, ProtocolState.Play)]
public class OpenBookPacket : IClientboundPacket
{
    public Hand Hand { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Hand = (Hand)buffer.ReadVarInt();
    }
}
