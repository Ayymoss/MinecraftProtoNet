using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Packets.Base;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Queues inbound Play-state packets and drains them on the game thread at tick start.
/// Mirrors vanilla Minecraft's PacketProcessor: network thread enqueues, game thread drains.
/// </summary>
/// <remarks>
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/PacketProcessor.java
/// </remarks>
public class PacketProcessor(ILogger<PacketProcessor> logger) : IPacketProcessor
{
    private readonly record struct QueuedPacket(IClientboundPacket Packet, IPacketHandler Handler, IMinecraftClient Client);

    private readonly ConcurrentQueue<QueuedPacket> _queue = new();
    private Thread? _gameThread;
    private volatile bool _closed;

    /// <inheritdoc />
    public bool IsActive => _gameThread != null;

    /// <inheritdoc />
    public void SetGameThread(Thread thread)
    {
        _gameThread = thread;
        logger.LogDebug("PacketProcessor: Game thread set to {ThreadName} (ID: {ThreadId})",
            thread.Name, thread.ManagedThreadId);
    }

    /// <inheritdoc />
    public bool IsSameThread()
    {
        return _gameThread != null && Environment.CurrentManagedThreadId == _gameThread.ManagedThreadId;
    }

    /// <inheritdoc />
    public void Enqueue(IClientboundPacket packet, IPacketHandler handler, IMinecraftClient client)
    {
        if (_closed)
        {
            logger.LogWarning("PacketProcessor: Discarding packet {PacketType} — processor is closed",
                packet.GetType().Name);
            return;
        }

        _queue.Enqueue(new QueuedPacket(packet, handler, client));

        // The queue is the gap between how fast the server talks and how fast we listen. Hypixel bursts over
        // 1,500 packets in a second on join and sustains 1,000+ in a crowded hub, and a backlog here means we
        // are not draining the socket — which is a plausible reason for a server to hang up on us. Reported
        // once per escalating threshold rather than per packet, so the diagnostic cannot itself be the stall.
        var depth = _queue.Count;
        if (depth >= _nextDepthWarning)
        {
            _nextDepthWarning = depth * 2;
            logger.LogWarning("PacketProcessor: {Depth} packets queued for the game thread — falling behind", depth);
        }
        else if (depth < 64)
        {
            _nextDepthWarning = 512;
        }
    }

    /// <summary>Next queue depth worth reporting; doubles each time so a sustained backlog does not spam.</summary>
    private int _nextDepthWarning = 512;

    /// <inheritdoc />
    public async Task ProcessQueuedPacketsAsync()
    {
        // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/PacketProcessor.java
        // processQueuedPackets() drains the entire queue, handling exceptions per-packet.
        while (_queue.TryDequeue(out var entry))
        {
            try
            {
                await entry.Handler.HandleAsync(entry.Packet, entry.Client);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PacketProcessor: Error handling queued packet {PacketType}",
                    entry.Packet.GetType().Name);
            }
        }
    }

    /// <inheritdoc />
    public void Close()
    {
        _closed = true;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _closed = false;
        _gameThread = null;

        // Drain any stale packets from a previous session
        while (_queue.TryDequeue(out _)) { }

        logger.LogDebug("PacketProcessor: Reset for new connection session");
    }
}
