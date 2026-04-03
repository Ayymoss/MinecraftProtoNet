using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Clears title/subtitle/actionbar text on the client.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundClearTitlesPacket.java
/// </summary>
[Packet(0x0E, ProtocolState.Play, silent: true)]
public class ClearTitlesPacket : IClientboundPacket
{
    public bool ResetTimes { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ResetTimes = buffer.ReadBoolean();
    }
}
