using MinecraftProtoNet.Core.NBT.Tags;

namespace MinecraftProtoNet.Core.Core.Abstractions;

/// <summary>
/// Event bus for system chat messages. Allows external systems (e.g., Bazaar trading)
/// to subscribe to chat messages without coupling to Core packet handlers.
/// </summary>
public interface IChatEventBus
{
    event Action<SystemChatEventArgs>? OnSystemChat;
    void PublishSystemChat(NbtTag tags, bool overlay, string? translateKey, List<string> textParts);
}

/// <summary>
/// Event args for system chat messages parsed from SystemChatPacket.
/// </summary>
public record SystemChatEventArgs(
    NbtTag Tags,
    bool Overlay,
    string? TranslateKey,
    List<string> TextParts
);
