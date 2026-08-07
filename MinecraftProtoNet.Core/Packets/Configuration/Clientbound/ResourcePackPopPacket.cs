using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Clientbound;

/// <summary>
/// Removes one resource pack by id, or all of them when no id is present. Configuration-phase counterpart of
/// the Play-state packet.
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/common/ClientboundResourcePackPopPacket.java
/// </summary>
[Packet(0x08, ProtocolState.Configuration)]
public class ResourcePackPopPacket : IClientboundPacket
{
    /// <summary>Present = drop that pack; absent = drop all packs.</summary>
    public Guid? PackId { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        if (buffer.ReadBoolean())
        {
            PackId = buffer.ReadUuid();
        }
    }
}
