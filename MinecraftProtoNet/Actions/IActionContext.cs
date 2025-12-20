using MinecraftProtoNet.Auth.Dtos;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.State.Base;

namespace MinecraftProtoNet.Actions;

/// <summary>
/// Provides the context required for executing actions.
/// Decoupled from any specific trigger mechanism (chat, console, API, etc.)
/// </summary>
public interface IActionContext
{
    /// <summary>
    /// The Minecraft client instance.
    /// </summary>
    IMinecraftClient Client { get; }

    /// <summary>
    /// The current client state including level, players, and local player.
    /// </summary>
    ClientState State { get; }

    /// <summary>
    /// Authentication result containing player credentials and chat session.
    /// </summary>
    AuthResult AuthResult { get; }

    /// <summary>
    /// Sends a packet to the server.
    /// </summary>
    Task SendPacketAsync(IServerboundPacket packet);

    /// <summary>
    /// Sends a signed chat message using the player's private key.
    /// </summary>
    Task SendSignedChatAsync(string message);

    /// <summary>
    /// Sends an unsigned chat message (may be rejected by servers requiring signatures).
    /// </summary>
    Task SendUnsignedChatAsync(string message);
}
