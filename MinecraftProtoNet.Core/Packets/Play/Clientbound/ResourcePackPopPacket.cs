using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Sent by the server to remove a resource pack (by ID) or all packs (if no ID).
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/common/ClientboundResourcePackPopPacket.java
/// </summary>
[Packet(0x50, ProtocolState.Play)]
public class ResourcePackPopPacket : IClientboundPacket
{
    /// <summary>
    /// If present, remove the specific pack. If absent, remove all packs.
    /// </summary>
    public Guid? PackId { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        var hasId = buffer.ReadBoolean();
        if (hasId)
        {
            PackId = buffer.ReadUuid();
        }
    }
}
