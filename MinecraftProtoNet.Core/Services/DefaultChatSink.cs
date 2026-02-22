using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Utilities;
using Serilog;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// A chat sink that sends messages directly to the Minecraft server.
/// </summary>
/// <param name="client">The Minecraft client instance.</param>
public sealed class DefaultChatSink(IMinecraftClient client) : IChatSink
{
    /// <inheritdoc />
    public async Task EmitAsync(string message, CancellationToken ct = default)
    {
        // Check if server enforces secure chat
        if (client.State.ServerSettings.EnforcesSecureChat && client.AuthResult is not null)
        {
            var packet = ChatSigning.CreateSignedChatPacket(client.AuthResult, message);
            if (packet == null)
            {
                Log.Warning("[WARN] Server requires signed chat but signing failed. Attempting unsigned");
                packet = new ChatPacket(message);
            }
            await client.SendPacketAsync(packet, ct);
        }
        else
        {
            // Server doesn't require signed chat - use unsigned
            await client.SendPacketAsync(new ChatPacket(message), ct);
        }
    }
}
