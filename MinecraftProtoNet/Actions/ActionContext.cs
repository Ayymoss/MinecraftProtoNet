using MinecraftProtoNet.Auth.Dtos;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.State.Base;
using MinecraftProtoNet.Utilities;

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

    public async Task SendSignedChatAsync(string message)
    {
        var packet = ChatSigning.CreateSignedChatPacket(AuthResult, message);
        if (packet == null)
        {
            Console.WriteLine("[WARN] Failed to create signed chat packet, falling back to unsigned.");
            packet = new ChatPacket(message);
        }
        await SendPacketAsync(packet);
    }

    public Task SendUnsignedChatAsync(string message)
    {
        return SendPacketAsync(new ChatPacket(message));
    }
}
