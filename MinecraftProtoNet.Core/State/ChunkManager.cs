using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Physics;
using MinecraftProtoNet.Physics.Shapes;

namespace MinecraftProtoNet.State;

/// <summary>
/// Manages chunk and block state.
/// </summary>
public class ChunkManager : IChunkManager
{
    private readonly ILogger _logger = LoggingConfiguration.CreateLogger("ChunkManager");

    private readonly IBlockShapeRegistry _blockShapeRegistry;

    public ChunkManager(IBlockShapeRegistry blockShapeRegistry)
    {
        _blockShapeRegistry = blockShapeRegistry;
    }
    
    // Default constructor for cases where DI might not provide it immediately or for tests, though strictly DI should be used.
    // If we enforce DI, we remove parameterless. But existing usages might break if they rely on "new ChunkManager()".
    // I will add a default that uses a basic registry if needed, or rely on DI.
    // For safety in this migration, I'll keep a parameterless one that creates a default registry.
    public ChunkManager() : this(new BlockShapeRegistry()) { }

    /// <inheritdoc />
    public ConcurrentDictionary<(int ChunkX, int ChunkZ), Chunk> Chunks { get; } = [];

    /// <inheritdoc />
    public BlockState? GetBlockAt(int worldX, int worldY, int worldZ)
    {
        var chunkX = worldX >> 4;
        var chunkZ = worldZ >> 4;

        if (!Chunks.TryGetValue((chunkX, chunkZ), out var chunk)) return null;

        return chunk.GetBlock(worldX, worldY, worldZ);
    }

    /// <inheritdoc />
    public void HandleBlockUpdate(Vector3<double> position, int blockStateId)
    {
        var x = (int)Math.Floor(position.X);
        var y = (int)Math.Floor(position.Y);
        var z = (int)Math.Floor(position.Z);

        var chunkX = x >> 4;
        var chunkZ = z >> 4;

        if (!Chunks.TryGetValue((chunkX, chunkZ), out var chunk)) return;

        chunk.SetBlock(x, y, z, blockStateId);
    }

    /// <inheritdoc />
    public IEnumerable<Physics.Shapes.VoxelShape> GetCollidingShapes(AABB queryBox)
    {
        var minBx = (int)Math.Floor(queryBox.MinX);
        var minBy = (int)Math.Floor(queryBox.MinY);
        var minBz = (int)Math.Floor(queryBox.MinZ);
        var maxBx = (int)Math.Floor(queryBox.MaxX);
        var maxBy = (int)Math.Floor(queryBox.MaxY);
        var maxBz = (int)Math.Floor(queryBox.MaxZ);

        for (var y = minBy; y <= maxBy; y++)
        {
            if (y is < -64 or > 319) continue;

            for (var x = minBx; x <= maxBx; x++)
            {
                for (var z = minBz; z <= maxBz; z++)
                {
                    var blockState = GetBlockAt(x, y, z);
                    if (blockState is not { BlocksMotion: true }) continue;

                    var shape = _blockShapeRegistry.GetShape(blockState);
                    if (!shape.IsEmpty())
                    {
                        var offsetShape = shape.Move(x, y, z);
                        // Check bounds intersection optimization?
                        // VoxelShape.Bounds() is relative to origin. Move works.
                        // We yield it.
                         yield return offsetShape;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public List<AABB> GetCollidingBlockAABBs(AABB queryBox)
    {
        var list = new List<AABB>();
        foreach (var shape in GetCollidingShapes(queryBox))
        {
             // VoxelShape consists of boxes.
             // We need to check intersection with queryBox.
             // VoxelShape.ToAABBs() returns all boxes.
             
             // Optimization: Shape already moved to world coords.
             foreach(var box in shape.ToAABBs())
             {
                 if (box.Intersects(queryBox))
                 {
                     list.Add(box);
                 }
             }
        }
        
        // Debugging specific block kept from original code
        if (list.Count == 0 && Math.Abs(queryBox.MinY - 82.2522) < 0.1)
        {
             var targetX = (int)Math.Floor(queryBox.MinX);
             var targetY = (int)Math.Floor(queryBox.MinY - 0.01);
             var targetZ = (int)Math.Floor(queryBox.MinZ);
             var block = GetBlockAt(targetX, targetY, targetZ);
             _logger.LogDebug("[TargetDebug] Block at ({X},{Y},{Z}) is {BlockName} (ID: {BlockId}), BlocksMotion={BM}", 
                 targetX, targetY, targetZ, block?.Name ?? "NULL", block?.Id ?? -1, block?.BlocksMotion ?? false);
        }

        return list;
    }

    /// <inheritdoc />
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
                InsideBlock = false, // Clip handles boundary usually
                BlockPosition = new Vector3<int>(x, y, z),
                ExactHitPosition = clipResult.Value.Point,
                Distance = (clipResult.Value.Point - start).Length()
            };
        }
        return null;
    }

    /// <inheritdoc />
    public void AddChunk(Chunk chunk)
    {
        Chunks[(chunk.X, chunk.Z)] = chunk;
    }

    /// <inheritdoc />
    public bool HasChunk(int chunkX, int chunkZ)
    {
        return Chunks.ContainsKey((chunkX, chunkZ));
    }

    /// <inheritdoc />
    public Chunk? GetChunk(int chunkX, int chunkZ)
    {
        Chunks.TryGetValue((chunkX, chunkZ), out var chunk);
        return chunk;
    }
}
