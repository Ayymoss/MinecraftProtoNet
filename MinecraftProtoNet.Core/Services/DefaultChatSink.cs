using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Utilities;
using Serilog;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// A chat sink that sends messages directly to the Minecraft server.
/// Applies humanized delays before sending and blocks non-command chat on remote servers.
/// </summary>
/// <param name="client">The Minecraft client instance.</param>
/// <param name="humanizer">Humanizer for timing delays and remote server detection.</param>
public sealed class DefaultChatSink(IMinecraftClient client, IHumanizer humanizer) : IChatSink
{
    /// <inheritdoc />
    public async Task EmitAsync(string message, CancellationToken ct = default)
    {
        // Safety: block non-slash messages on remote servers to prevent accidental chat leaks
        if (humanizer.IsRemoteServer && !message.StartsWith('/'))
        {
            Log.Warning("[ChatSink] Blocked non-command message on remote server: {Message}", message);
            return;
        }

        // Humanized delay before sending (simulates human reaction/typing time)
        var delayMs = humanizer.GetChatCommandDelayMs();
        if (delayMs > 0)
            await Task.Delay(delayMs, ct);

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
