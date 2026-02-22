using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// A chat sink that sends messages directly to the Minecraft server.
/// </summary>
/// <param name="sender">The packet sender used to transmit chat packets.</param>
public sealed class DefaultChatSink(IPacketSender sender) : IChatSink
{
    /// <inheritdoc />
    public async Task EmitAsync(string message, CancellationToken ct = default)
    {
        var packet = new ChatPacket(message);
        await sender.SendPacketAsync(packet, ct);
    }
}
