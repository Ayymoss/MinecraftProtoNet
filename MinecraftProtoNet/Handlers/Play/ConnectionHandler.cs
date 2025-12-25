using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Services;
using KeepAlivePacket = MinecraftProtoNet.Packets.Play.Clientbound.KeepAlivePacket;

namespace MinecraftProtoNet.Handlers.Play;

/// <summary>
/// Handles keep-alive, ping, and connection maintenance packets.
/// </summary>
[HandlesPacket(typeof(KeepAlivePacket))]
[HandlesPacket(typeof(PingPacket))]
[HandlesPacket(typeof(PongResponsePacket))]
[HandlesPacket(typeof(ChunkBatchFinishedPacket))]
public class ConnectionHandler(ILogger<ConnectionHandler> logger) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(ConnectionHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case KeepAlivePacket keepAlivePacket:
                await client.SendPacketAsync(new Packets.Play.Serverbound.KeepAlivePacket
                {
                    Payload = keepAlivePacket.Payload
                });
                break;
                
            case PingPacket pingPacket:
                await client.SendPacketAsync(new PongPacket { Payload = pingPacket.Id });
                break;
                
            case PongResponsePacket pongResponsePacket:
                logger.LogDebug("Pong response: {Payload}", pongResponsePacket.Payload);
                break;
                
            case ChunkBatchFinishedPacket chunkBatchFinishedPacket:
                await client.SendPacketAsync(new ChunkBatchReceivedPacket
                {
                    DesiredChunksPerTick = chunkBatchFinishedPacket.BatchSize
                });
                break;
        }
    }
}
