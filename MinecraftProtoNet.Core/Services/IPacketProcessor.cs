using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Packets.Base;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Queues inbound packets during Play state and drains them on the game thread at tick start,
/// matching vanilla Minecraft's PacketProcessor architecture.
/// </summary>
/// <remarks>
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/PacketProcessor.java
/// </remarks>
public interface IPacketProcessor
{
    /// <summary>
    /// Whether the game thread has been set (i.e., GameLoop is running and packets should be queued).
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Registers the game loop thread for same-thread checks.
    /// </summary>
    void SetGameThread(Thread thread);

    /// <summary>
    /// Returns true if the current thread is the registered game thread.
    /// </summary>
    bool IsSameThread();

    /// <summary>
    /// Enqueues a packet for deferred handling on the game thread.
    /// </summary>
    void Enqueue(IClientboundPacket packet, IPacketHandler handler, IMinecraftClient client);

    /// <summary>
    /// Drains and handles all queued packets. Must be called from the game thread.
    /// </summary>
    Task ProcessQueuedPacketsAsync();

    /// <summary>
    /// Prevents further enqueuing (called on disconnect/shutdown).
    /// </summary>
    void Close();

    /// <summary>
    /// Clears the queue and resets state for a new connection session.
    /// </summary>
    void Reset();
}
