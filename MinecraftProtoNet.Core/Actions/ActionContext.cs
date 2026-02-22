using MinecraftProtoNet.Core.Auth.Dtos;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.State.Base;
using MinecraftProtoNet.Core.Utilities;
using Serilog;

namespace MinecraftProtoNet.Core.Actions;

/// <summary>
/// Default implementation of IActionContext providing access to client state and common operations.
/// </summary>
public class ActionContext(IMinecraftClient client, ClientState state, AuthResult authResult) : IActionContext
{
    public IMinecraftClient Client { get; } = client ?? throw new ArgumentNullException(nameof(client));
    public ClientState State { get; } = state ?? throw new ArgumentNullException(nameof(state));
    public AuthResult AuthResult { get; } = authResult ?? throw new ArgumentNullException(nameof(authResult));

    public Task SendPacketAsync(IServerboundPacket packet)
    {
        return Client.SendPacketAsync(packet);
    }

    /// <summary>
    /// Sends a chat message. Automatically handles redirection if configured in bot settings.
    /// </summary>
    /// <param name="message">The message to send.</param>
    public async Task SendChatAsync(string message)
    {
        await Client.SendChatMessageAsync(message);
    }
}
