using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Displays text above the hotbar (action bar).
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetActionBarTextPacket.java
/// </summary>
[Packet(0x57, ProtocolState.Play, silent: true)]
public class SetActionBarTextPacket : IClientboundPacket
{
    public string Text { get; set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Text = buffer.ReadChatComponent();
    }
}
