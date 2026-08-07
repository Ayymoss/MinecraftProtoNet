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

    /// <summary>
    /// Title, subtitle and action-bar text. Kept separate from chat because it never appears in the chat log
    /// a player would scroll back through — which is exactly why staff use it to address someone.
    /// </summary>
    event Action<ScreenTextEventArgs>? OnScreenText;
    void PublishScreenText(ScreenTextKind kind, string text);
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

public enum ScreenTextKind
{
    Title,
    Subtitle,
    ActionBar,

    /// <summary>A written book the server forced open — never something the bot asked for.</summary>
    Book,

    /// <summary>A server-defined dialog screen pushed at the client.</summary>
    Dialog
}

public record ScreenTextEventArgs(ScreenTextKind Kind, string Text);
