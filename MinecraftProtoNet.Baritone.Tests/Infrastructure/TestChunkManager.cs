using System.Collections.Concurrent;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Physics;
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

    private readonly IBlockShapeRegistry _blockShapeRegistry = new BlockShapeRegistry();

    public IEnumerable<VoxelShape> GetCollidingShapes(AABB queryBox)
    {
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
            if (block is { BlocksMotion: true, IsAir: false })
            {
                var shape = _blockShapeRegistry.GetShape(block);
                if (!shape.IsEmpty())
                {
                    var movedShape = shape.Move(x, y, z);
                    // Check if the shape actually intersects the queryBox
                    if (queryBox.Intersects(movedShape.Bounds()))
                    {
                        yield return movedShape;
                    }
                }
            }
        }
    }
    
    public RaycastHit? RayCast(Vector3<double> start, Vector3<double> direction, double maxDistance = 100.0)
    {
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
        if (length < 1e-10) return null;

        var normDir = new Vector3<double>(direction.X / length, direction.Y / length, direction.Z / length);
        var end = start + normDir * maxDistance;

        var x = (int)Math.Floor(start.X);
        var y = (int)Math.Floor(start.Y);
        var z = (int)Math.Floor(start.Z);

        var stepX = normDir.X > 0 ? 1 : -1;
        var stepY = normDir.Y > 0 ? 1 : -1;
        var stepZ = normDir.Z > 0 ? 1 : -1;

        var tMaxX = normDir.X != 0 ? (stepX > 0 ? x + 1 - start.X : start.X - x) / Math.Abs(normDir.X) : double.MaxValue;
        var tMaxY = normDir.Y != 0 ? (stepY > 0 ? y + 1 - start.Y : start.Y - y) / Math.Abs(normDir.Y) : double.MaxValue;
        var tMaxZ = normDir.Z != 0 ? (stepZ > 0 ? z + 1 - start.Z : start.Z - z) / Math.Abs(normDir.Z) : double.MaxValue;

        var tDeltaX = normDir.X != 0 ? Math.Abs(1.0 / normDir.X) : double.MaxValue;
        var tDeltaY = normDir.Y != 0 ? Math.Abs(1.0 / normDir.Y) : double.MaxValue;
        var tDeltaZ = normDir.Z != 0 ? Math.Abs(1.0 / normDir.Z) : double.MaxValue;

        // Check start block first
        var hit = CheckBlock(x, y, z, start, end);
        if (hit != null) return hit;

        var maxSteps = Math.Max(10, (int)(maxDistance * 3)); // Heuristic limit
        var steps = 0;

        while (steps < maxSteps)
        {
            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                if (tMaxX > maxDistance) break;
                x += stepX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxZ)
            {
                if (tMaxY > maxDistance) break;
                y += stepY;
                tMaxY += tDeltaY;
            }
            else
            {
                 if (tMaxZ > maxDistance) break;
                z += stepZ;
                tMaxZ += tDeltaZ;
            }

            hit = CheckBlock(x, y, z, start, end);
            if (hit != null) return hit;

            steps++;
        }

        return null;
    }

    private RaycastHit? CheckBlock(int x, int y, int z, Vector3<double> start, Vector3<double> end)
    {
        var block = GetBlockAt(x, y, z);
        if (block == null || block.IsAir || block.IsLiquid) return null;

        var shape = _blockShapeRegistry.GetShape(block);
        if (shape.IsEmpty()) return null;

        var clipResult = shape.Clip(start, end, new Vector3<int>(x, y, z));
        if (clipResult != null && clipResult.Value.Hit)
        {
            return new RaycastHit
            {
                Block = block,
                Face = clipResult.Value.Face,
                InsideBlock = false,
                BlockPosition = new Vector3<int>(x, y, z),
                ExactHitPosition = clipResult.Value.Point,
                Distance = (clipResult.Value.Point - start).Length()
            };
        }
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
