using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Clientbound;

/// <summary>
/// Server asks the client to download a resource pack, during the configuration phase.
///
/// Identical payload to the Play-state packet — vanilla defines it once as a *common* packet registered into
/// both protocols, so only the id differs.
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/common/ClientboundResourcePackPushPacket.java
///
/// This must be answered. Servers that push a pack during configuration hold the connection there until the
/// client replies, so an unhandled push leaves the client stuck in Configuration answering keep-alives forever
/// and never reaching Play.
/// </summary>
[Packet(0x09, ProtocolState.Configuration)]
public class ResourcePackPushPacket : IClientboundPacket
{
    public Guid PackId { get; set; }

    public string Url { get; set; } = string.Empty;

    /// <summary>SHA-1 hash of the pack file (max 40 chars).</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>If true, the server disconnects clients that decline.</summary>
    public bool Required { get; set; }

    public bool HasPrompt { get; set; }

    public string? Prompt { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        PackId = buffer.ReadUuid();
        Url = buffer.ReadString();
        Hash = buffer.ReadString();
        Required = buffer.ReadBoolean();
        HasPrompt = buffer.ReadBoolean();
        if (HasPrompt)
        {
            Prompt = buffer.ReadChatComponent();
        }
    }
}
