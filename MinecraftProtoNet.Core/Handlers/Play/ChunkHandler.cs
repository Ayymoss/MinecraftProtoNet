using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Services;

namespace MinecraftProtoNet.Core.Handlers.Play;

/// <summary>
/// Handles chunk and block-related packets.
/// </summary>
[HandlesPacket(typeof(LevelChunkWithLightPacket))]
[HandlesPacket(typeof(ForgetLevelChunkPacket))]
[HandlesPacket(typeof(BlockUpdatePacket))]
[HandlesPacket(typeof(SetChunkCacheCenterPacket))]
public class ChunkHandler(ILogger<ChunkHandler> logger) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(ChunkHandler));

    public Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case LevelChunkWithLightPacket levelChunkWithLightPacket:
                client.State.Level.AddChunk(levelChunkWithLightPacket.Chunk);
                break;
                
            case ForgetLevelChunkPacket forgetLevelChunkPacket:
                client.State.Level.Chunks.TryRemove(
                    (forgetLevelChunkPacket.ChunkX, forgetLevelChunkPacket.ChunkZ), out _);
                break;
                
            case BlockUpdatePacket blockUpdatePacket:
                client.State.Level.HandleBlockUpdate(blockUpdatePacket.Position, blockUpdatePacket.BlockId);
                break;
                
            case SetChunkCacheCenterPacket setChunkCacheCenterPacket:
                logger.LogDebug("Chunk cache center: {X}, {Z}",
                    setChunkCacheCenterPacket.ChunkX, setChunkCacheCenterPacket.ChunkZ);
                break;
        }

        return Task.CompletedTask;
    }
}
