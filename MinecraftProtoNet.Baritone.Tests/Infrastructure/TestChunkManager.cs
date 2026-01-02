using System.Collections.Concurrent;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Physics.Shapes;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Baritone.Tests.Infrastructure;

/// <summary>
/// A test implementation of IChunkManager for deterministic block data in unit tests.
/// </summary>
public class TestChunkManager : IChunkManager
{
    private readonly Dictionary<(int X, int Y, int Z), BlockState> _blocks = new();
    private readonly ConcurrentDictionary<(int ChunkX, int ChunkZ), Chunk> _chunks = new();
    
    public ConcurrentDictionary<(int ChunkX, int ChunkZ), Chunk> Chunks => _chunks;

    /// <summary>
    /// Sets a block at the specified world coordinates.
    /// </summary>
    public void SetBlock(int worldX, int worldY, int worldZ, BlockState state)
    {
        _blocks[(worldX, worldY, worldZ)] = state;
    }

    /// <summary>
    /// Sets a block by name (convenience method for common blocks).
    /// </summary>
    public void SetBlock(int worldX, int worldY, int worldZ, string blockName, bool hasCollision = true)
    {
        var state = new BlockState(GetIdFromName(blockName), blockName)
        {
            HasCollision = hasCollision
        };
        SetBlock(worldX, worldY, worldZ, state);
    }

    /// <summary>
    /// Fills a region with a block type.
    /// </summary>
    public void Fill(int x1, int y1, int z1, int x2, int y2, int z2, string blockName, bool hasCollision = true)
    {
        var (minX, maxX) = (Math.Min(x1, x2), Math.Max(x1, x2));
        var (minY, maxY) = (Math.Min(y1, y2), Math.Max(y1, y2));
        var (minZ, maxZ) = (Math.Min(z1, z2), Math.Max(z1, z2));

        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            SetBlock(x, y, z, blockName, hasCollision);
        }
    }

    /// <summary>
    /// Creates a flat floor at the specified Y level.
    /// </summary>
    public void CreateFloor(int y, int halfWidth = 10, string blockName = "minecraft:stone")
    {
        Fill(-halfWidth, y, -halfWidth, halfWidth, y, halfWidth, blockName);
    }

    public BlockState? GetBlockAt(int worldX, int worldY, int worldZ)
    {
        if (_blocks.TryGetValue((worldX, worldY, worldZ), out var block))
        {
            return block;
        }
        
        // Return air for unset blocks
        return new BlockState(0, "minecraft:air") { HasCollision = false };
    }

    public void HandleBlockUpdate(Vector3<double> position, int blockStateId)
    {
        // For testing, we can implement if needed
    }

    public List<AABB> GetCollidingBlockAABBs(AABB queryBox)
    {
        var result = new List<AABB>();
        
        int minX = (int)Math.Floor(queryBox.Min.X);
        int maxX = (int)Math.Ceiling(queryBox.Max.X);
        int minY = (int)Math.Floor(queryBox.Min.Y);
        int maxY = (int)Math.Ceiling(queryBox.Max.Y);
        int minZ = (int)Math.Floor(queryBox.Min.Z);
        int maxZ = (int)Math.Ceiling(queryBox.Max.Z);

        for (int x = minX; x < maxX; x++)
        for (int y = minY; y < maxY; y++)
        for (int z = minZ; z < maxZ; z++)
        {
            var block = GetBlockAt(x, y, z);
            if (block is { HasCollision: true, IsAir: false })
            {
                var blockBox = new AABB(x, y, z, x + 1, y + 1, z + 1);
                if (queryBox.Intersects(blockBox))
                {
                    result.Add(blockBox);
                }
            }
        }

        return result;
    }

    public IEnumerable<VoxelShape> GetCollidingShapes(AABB queryBox)
    {
        var aabbs = GetCollidingBlockAABBs(queryBox);
        foreach (var box in aabbs)
        {
            yield return Shapes.Block().Move(box.Min.X, box.Min.Y, box.Min.Z);
        }
    }
    
    public RaycastHit? RayCast(Vector3<double> start, Vector3<double> direction, double maxDistance = 100.0)
    {
        // Simplified raycast for testing
        return null;
    }

    public void AddChunk(Chunk chunk)
    {
        _chunks[(chunk.X, chunk.Z)] = chunk;
    }

    public bool HasChunk(int chunkX, int chunkZ) => _chunks.ContainsKey((chunkX, chunkZ));

    public Chunk? GetChunk(int chunkX, int chunkZ) => 
        _chunks.TryGetValue((chunkX, chunkZ), out var chunk) ? chunk : null;

    private static int GetIdFromName(string name) => name switch
    {
        "minecraft:air" => 0,
        "minecraft:stone" => 1,
        "minecraft:grass_block" => 2,
        "minecraft:dirt" => 3,
        "minecraft:cobblestone" => 4,
        "minecraft:water" => 5,
        "minecraft:lava" => 6,
        _ => name.GetHashCode() & 0x7FFF
    };
}
