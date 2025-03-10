using System.Collections.Concurrent;
using MinecraftProtoNet.Models.World.Chunk;

namespace MinecraftProtoNet.State;

public class Level
{
    public ConcurrentDictionary<(int ChunkX, int ChunkZ), Chunk> Chunks { get; set; } = [];

    public BlockState? GetBlockAt(int worldX, int worldY, int worldZ)
    {
        var chunkX = (int)Math.Floor((double)worldX / 16);
        var chunkZ = (int)Math.Floor((double)worldZ / 16);

        if (!Chunks.TryGetValue((chunkX, chunkZ), out var chunk)) return null;

        var localX = worldX & 0xF;
        var localZ = worldZ & 0xF;

        return chunk.GetBlock(localX, worldY, localZ);
    }

    public void AddChunk(Chunk chunk)
    {
        Chunks[(chunk.X, chunk.Z)] = chunk;
    }

    public bool HasChunk(int chunkX, int chunkZ)
    {
        return Chunks.ContainsKey((chunkX, chunkZ));
    }

    public Chunk? GetChunk(int chunkX, int chunkZ)
    {
        Chunks.TryGetValue((chunkX, chunkZ), out var chunk);
        return chunk;
    }
}
