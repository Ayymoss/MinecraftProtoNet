using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Big centre-screen text. Staff use titles to get a player's attention, so the bot has to be able to read them, not just skip the bytes.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetTitleTextPacket.java
/// </summary>
[Packet(0x72, ProtocolState.Play, silent: true)]
public class SetTitleTextPacket : IClientboundPacket
{
    /// <summary>Plain text of the component, formatting codes stripped by the reader.</summary>
    public string Text { get; set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Text = buffer.ReadChatComponent();
    }
}
