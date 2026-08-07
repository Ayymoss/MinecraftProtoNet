using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
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

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case LevelChunkWithLightPacket levelChunkWithLightPacket:
                client.State.Level.AddChunk(levelChunkWithLightPacket.Chunk);

                // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:2536-2551
                // Vanilla sends PlayerLoadedPacket when levelLoadTracker.isLevelReady() — i.e., after
                // the first chunks around the player are loaded. We approximate by sending on first chunk.
                //
                // The "already sent" flag is per-LEVEL, on client state, and is cleared by handleLogin and
                // handleRespawn. It used to be a field on this handler, which DI keeps alive for the whole
                // process: the packet was therefore sent for the first world only, and after any server switch
                // the new backend never learned the client had loaded and pinned the player at spawn.
                if (!client.State.ClientLoadedNotified)
                {
                    client.State.ClientLoadedNotified = true;
                    await client.SendPacketAsync(new PlayerLoadedPacket());
                    logger.LogInformation("Sent PlayerLoadedPacket after first chunk of this level");
                }
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

    }
}
