using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Actions;

/// <summary>
/// Actions related to chat and server commands.
/// </summary>
public static class ChatActions
{
    /// <summary>
    /// Sends a signed chat message using the player's private key.
    /// </summary>
    public static Task SendSignedMessageAsync(IActionContext ctx, string message)
    {
        return ctx.SendSignedChatAsync(message);
    }

    /// <summary>
    /// Sends an unsigned chat message.
    /// </summary>
    public static Task SendUnsignedMessageAsync(IActionContext ctx, string message)
    {
        return ctx.SendUnsignedChatAsync(message);
    }

    /// <summary>
    /// Sends a server command (without the leading slash).
    /// </summary>
    public static Task SendCommandAsync(IActionContext ctx, string command)
    {
        return ctx.SendPacketAsync(new ChatCommandPacket(command));
    }
}
