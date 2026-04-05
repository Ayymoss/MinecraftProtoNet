using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

/// <summary>
/// Sent by the client in response to a ResourcePackPushPacket.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/common/ServerboundResourcePackPacket.java
/// </summary>
[Packet(0x31, ProtocolState.Play)]
public class ResourcePackPacket : IServerboundPacket
{
    public required Guid PackId { get; set; }
    public required ResourcePackAction Action { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteUUID(PackId);
        buffer.WriteVarInt((int)Action);
    }

    /// <summary>
    /// Action enum matching vanilla's ServerboundResourcePackPacket.Action ordinal order.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/common/ServerboundResourcePackPacket.java:29-37
    /// </summary>
    public enum ResourcePackAction
    {
        SuccessfullyLoaded = 0,
        Declined = 1,
        FailedDownload = 2,
        Accepted = 3,
        Downloaded = 4,
        InvalidUrl = 5,
        FailedReload = 6,
        Discarded = 7
    }
}
