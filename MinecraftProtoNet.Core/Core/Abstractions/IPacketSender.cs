using MinecraftProtoNet.Core.Packets.Base;

namespace MinecraftProtoNet.Core.Core.Abstractions;

/// <summary>
/// Minimal interface for sending packets to the server.
/// Breaks circular dependencies between high-level logic and the main client.
/// </summary>
public interface IPacketSender
{
    /// <summary>
    /// Sends a packet to the server asynchronously.
    /// </summary>
    Task SendPacketAsync(IServerboundPacket packet, CancellationToken cancellationToken = default);
}
