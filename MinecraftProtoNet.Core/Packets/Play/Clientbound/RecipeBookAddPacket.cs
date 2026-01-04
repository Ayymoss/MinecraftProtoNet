using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Adds recipes to the client's recipe book. Contains a list of recipe display entries.
/// This is a stub implementation that consumes the data without full parsing due to
/// the complex nested structure of recipe displays.
/// </summary>
[Packet(0x49, ProtocolState.Play)]
public class RecipeBookAddPacket : IClientboundPacket
{
    public bool Replace { get; set; }
    public int EntryCount { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        // This packet has a very complex structure with nested recipe displays.
        // Rather than risk parsing errors, we consume the remaining buffer.
        // The packet structure is: List<Entry> entries, bool replace
        // where Entry contains RecipeDisplayEntry + flags byte.
        
        // Skip all remaining bytes - the data is complex and we don't need it
        // to maintain connection stability. The 'replace' boolean is at the end
        // so we can't easily extract it without parsing everything.
        _ = buffer.ReadRestBuffer();
        
        EntryCount = 0;
        Replace = false;
    }
}

