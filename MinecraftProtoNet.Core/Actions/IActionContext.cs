using MinecraftProtoNet.Core.Auth.Dtos;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Actions;

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
    /// Sends a chat message. Automatically uses signed or unsigned chat based on server configuration.
    /// </summary>
    Task SendChatAsync(string message);
}
