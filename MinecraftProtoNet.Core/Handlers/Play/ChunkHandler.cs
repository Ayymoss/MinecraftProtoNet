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
[HandlesPacket(typeof(BlockEntityDataPacket))]
[HandlesPacket(typeof(SectionBlocksUpdatePacket))]
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
                // Store block entity NBT data (signs, chests, etc.)
                foreach (var be in levelChunkWithLightPacket.BlockEntities)
                {
                    var worldPos = new Models.Core.Vector3<int>(
                        levelChunkWithLightPacket.ChunkX * 16 + be.X,
                        be.Y,
                        levelChunkWithLightPacket.ChunkZ * 16 + be.Z);
                    client.State.Level.SetBlockEntity(worldPos, be.Type, be.Nbt);
                }
                break;
                
            case ForgetLevelChunkPacket forgetLevelChunkPacket:
                client.State.Level.Chunks.TryRemove(
                    (forgetLevelChunkPacket.ChunkX, forgetLevelChunkPacket.ChunkZ), out _);
                break;
                
            case BlockUpdatePacket blockUpdatePacket:
                client.State.Level.HandleBlockUpdate(blockUpdatePacket.Position, blockUpdatePacket.BlockId);
                break;

            case BlockEntityDataPacket blockEntityDataPacket:
                client.State.Level.SetBlockEntity(
                    blockEntityDataPacket.Position,
                    blockEntityDataPacket.BlockEntityType,
                    blockEntityDataPacket.Nbt);
                break;

            case SectionBlocksUpdatePacket sectionBlocksUpdatePacket:
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:787-789
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSectionBlocksUpdatePacket.java:44-48
                // Each packed long: bottom 12 bits = position within section, top bits = block state ID
                // Position bits: x = (pos >> 8) & 0xF, z = (pos >> 4) & 0xF, y = pos & 0xF
                var sectionX = (int)sectionBlocksUpdatePacket.SectionPosition.X;
                var sectionY = (int)sectionBlocksUpdatePacket.SectionPosition.Y;
                var sectionZ = (int)sectionBlocksUpdatePacket.SectionPosition.Z;
                foreach (var packed in sectionBlocksUpdatePacket.Blocks)
                {
                    var blockStateId = (int)(packed >>> 12);
                    var packedPos = (int)(packed & 0xFFF);
                    var worldX = sectionX * 16 + ((packedPos >> 8) & 0xF);
                    var worldY = sectionY * 16 + (packedPos & 0xF);
                    var worldZ = sectionZ * 16 + ((packedPos >> 4) & 0xF);
                    client.State.Level.HandleBlockUpdate(
                        new Models.Core.Vector3<double>(worldX, worldY, worldZ), blockStateId);
                }
                break;

            case SetChunkCacheCenterPacket setChunkCacheCenterPacket:
                logger.LogDebug("Chunk cache center: {X}, {Z}",
                    setChunkCacheCenterPacket.ChunkX, setChunkCacheCenterPacket.ChunkZ);
                break;
        }

        return Task.CompletedTask;
    }
}
