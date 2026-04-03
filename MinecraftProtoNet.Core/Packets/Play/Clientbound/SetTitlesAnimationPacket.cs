using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Sets title fade-in, stay, and fade-out timing.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetTitlesAnimationPacket.java
/// </summary>
[Packet(0x73, ProtocolState.Play, silent: true)]
public class SetTitlesAnimationPacket : IClientboundPacket
{
    public int FadeIn { get; set; }
    public int Stay { get; set; }
    public int FadeOut { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        FadeIn = buffer.ReadSignedInt();
        Stay = buffer.ReadSignedInt();
        FadeOut = buffer.ReadSignedInt();
    }
}
