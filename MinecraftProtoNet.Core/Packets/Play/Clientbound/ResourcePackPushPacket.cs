using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Sent by the server to request the client downloads a resource pack.
/// The client should respond with ServerboundResourcePackPacket.
/// </summary>
[Packet(0x50, ProtocolState.Play)]
public class ResourcePackPushPacket : IClientboundPacket
{
    /// <summary>
    /// Unique identifier for this resource pack.
    /// </summary>
    public Guid PackId { get; set; }
    
    /// <summary>
    /// The download URL for the resource pack.
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// SHA-1 hash of the resource pack file (max 40 chars).
    /// </summary>
    public string Hash { get; set; } = string.Empty;
    
    /// <summary>
    /// If true, the server will disconnect clients that decline.
    /// </summary>
    public bool Required { get; set; }
    
    /// <summary>
    /// Whether a custom prompt message is present.
    /// </summary>
    public bool HasPrompt { get; set; }
    
    /// <summary>
    /// Optional custom prompt message to display.
    /// </summary>
    public string? Prompt { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        PackId = buffer.ReadUuid();
        Url = buffer.ReadString();
        Hash = buffer.ReadString();  // Max 40 chars but not enforced at read time
        Required = buffer.ReadBoolean();
        HasPrompt = buffer.ReadBoolean();
        if (HasPrompt)
        {
            Prompt = buffer.ReadChatComponent();
        }
    }
}
