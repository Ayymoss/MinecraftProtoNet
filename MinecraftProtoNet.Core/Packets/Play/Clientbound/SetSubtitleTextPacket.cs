using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Sets the subtitle text displayed on the client.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetSubtitleTextPacket.java
/// </summary>
[Packet(0x70, ProtocolState.Play, silent: true)]
public class SetSubtitleTextPacket : IClientboundPacket
{
    /// <summary>Plain text of the component, formatting codes stripped by the reader.</summary>
    public string Text { get; set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Text = buffer.ReadChatComponent();
    }
}
