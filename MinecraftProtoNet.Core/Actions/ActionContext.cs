using MinecraftProtoNet.Auth.Dtos;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.State.Base;
using MinecraftProtoNet.Utilities;
using Serilog;

namespace MinecraftProtoNet.Actions;

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
    /// Sends a chat message. Automatically uses signed or unsigned chat based on server configuration.
    /// </summary>
    /// <param name="message">The message to send.</param>
    public async Task SendChatAsync(string message)
    {
        // Check if server enforces secure chat
        if (State.ServerSettings.EnforcesSecureChat)
        {
            var packet = ChatSigning.CreateSignedChatPacket(AuthResult, message);
            if (packet == null)
            {
                Log.Warning("[WARN] Server requires signed chat but signing failed. Attempting unsigned");
                packet = new ChatPacket(message);
            }
            await SendPacketAsync(packet);
        }
        else
        {
            // Server doesn't require signed chat - use unsigned
            await SendPacketAsync(new ChatPacket(message));
        }
    }
}
