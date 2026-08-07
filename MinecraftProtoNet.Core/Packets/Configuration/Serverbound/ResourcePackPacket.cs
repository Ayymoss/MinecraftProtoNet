using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Serverbound;

/// <summary>
/// Client's answer to a configuration-phase resource pack push.
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/common/ServerboundResourcePackPacket.java
///
/// Same payload as the Play-state response; the action ordinals are shared, so this reuses
/// <see cref="Play.Serverbound.ResourcePackPacket.ResourcePackAction"/> rather than redeclaring them.
/// </summary>
[Packet(0x06, ProtocolState.Configuration)]
public class ResourcePackPacket : IServerboundPacket
{
    public required Guid PackId { get; set; }
    public required Play.Serverbound.ResourcePackPacket.ResourcePackAction Action { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteUUID(PackId);
        buffer.WriteVarInt((int)Action);
    }
}
