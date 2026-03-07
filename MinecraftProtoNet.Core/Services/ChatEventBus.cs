using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.NBT.Tags;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Singleton implementation of IChatEventBus. Publishes system chat events
/// that external systems can subscribe to.
/// </summary>
public sealed class ChatEventBus : IChatEventBus
{
    public event Action<SystemChatEventArgs>? OnSystemChat;

    public void PublishSystemChat(NbtTag tags, bool overlay, string? translateKey, List<string> textParts)
    {
        OnSystemChat?.Invoke(new SystemChatEventArgs(tags, overlay, translateKey, textParts));
    }
}
