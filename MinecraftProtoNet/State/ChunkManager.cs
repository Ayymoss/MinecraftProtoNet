using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;

namespace MinecraftProtoNet.State;

/// <summary>
/// Manages chunk and block state.
/// </summary>
public class ChunkManager : IChunkManager
{
    private readonly ILogger _logger = LoggingConfiguration.CreateLogger("ChunkManager");

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
    public List<AABB> GetCollidingBlockAABBs(AABB queryBox)
    {
        var collidingBoxes = new List<AABB>();

        var minBx = (int)Math.Floor(queryBox.Min.X);
        var minBy = (int)Math.Floor(queryBox.Min.Y);
        var minBz = (int)Math.Floor(queryBox.Min.Z);
        var maxBx = (int)Math.Floor(queryBox.Max.X);
        var maxBy = (int)Math.Floor(queryBox.Max.Y);
        var maxBz = (int)Math.Floor(queryBox.Max.Z);

        for (var y = minBy; y <= maxBy; y++)
        {
            if (y is < -64 or > 319) continue;

            for (var x = minBx; x <= maxBx; x++)
            {
                for (var z = minBz; z <= maxBz; z++)
                {
                    var blockState = GetBlockAt(x, y, z);
                    if (blockState is not { BlocksMotion: true }) continue;

                    // I'm only supporting floor slabs for now. Everything else is a full block.
                    var blockBox = new AABB(x, y, z, x + 1, y + 1, z + 1);

                    if (blockState.IsSlab)
                    {
                        var type = blockState.Properties.GetValueOrDefault("type", "bottom");
                        if (type == "top")
                            blockBox = new AABB(x, y + 0.5, z, x + 1, y + 1, z + 1);
                        else if (type == "bottom")
                            blockBox = new AABB(x, y, z, x + 1, y + 0.5, z + 1);
                        // double is handled by default full blockbox
                    }
                    else if (blockState.IsSnow)
                    {
                        var layers = blockState.SnowLayers;
                        if (layers > 0)
                            blockBox = new AABB(x, y, z, x + 1, y + layers * 0.125, z + 1);
                        else continue; 
                    }
                    else if (blockState.IsStairs)
                    {
                        var isTop = blockState.IsTop;
                        // Base part
                        var baseBox = isTop ? new AABB(x, y + 0.5, z, x + 1, y + 1, z + 1) : new AABB(x, y, z, x + 1, y + 0.5, z + 1);
                        if (baseBox.Intersects(queryBox)) collidingBoxes.Add(baseBox);
                        
                        // Top part (simplification: always exists for now, we'll refine facing later if needed)
                        // In reality, it depends on 'facing' and 'shape'. 
                        // For stepping up, even this base is enough to trigger the step-up logic.
                        continue; 
                    }

                    if (blockBox.Intersects(queryBox))
                    {
                        collidingBoxes.Add(blockBox);
                        // Log "phantom" collisions (if needed) or just high counts
                    }
                    else
                    {
                        // Optional: Log misses if we are debugging a specific block
                        // _logger.LogTrace("Block {Name} at {X},{Y},{Z} does not intersect {Query}", blockState.Name, x, y, z, queryBox);
                    }
                }
            }
        }

        if (collidingBoxes.Count == 0 && Math.Abs(queryBox.Min.Y - 82.2522) < 0.1)
        {
             // Debugging the specific spot the user mentioned
             var targetX = (int)Math.Floor(queryBox.Min.X);
             var targetY = (int)Math.Floor(queryBox.Min.Y - 0.01);
             var targetZ = (int)Math.Floor(queryBox.Min.Z);
             var block = GetBlockAt(targetX, targetY, targetZ);
             _logger.LogDebug("[TargetDebug] Block at ({X},{Y},{Z}) is {BlockName} (ID: {BlockId}), BlocksMotion={BM}", 
                 targetX, targetY, targetZ, block?.Name ?? "NULL", block?.Id ?? -1, block?.BlocksMotion ?? false);
        }

        return collidingBoxes;
    }

    /// <inheritdoc />
    public RaycastHit? RayCast(Vector3<double> start, Vector3<double> direction, double maxDistance = 100.0)
    {
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
        if (length < 1e-10) return null;

        direction = new Vector3<double>(direction.X / length, direction.Y / length, direction.Z / length);

        var x = (int)Math.Floor(start.X);
        var y = (int)Math.Floor(start.Y);
        var z = (int)Math.Floor(start.Z);

        var stepX = direction.X > 0 ? 1 : -1;
        var stepY = direction.Y > 0 ? 1 : -1;
        var stepZ = direction.Z > 0 ? 1 : -1;

        var tMaxX = direction.X != 0 ? (stepX > 0 ? x + 1 - start.X : start.X - x) / Math.Abs(direction.X) : double.MaxValue;
        var tMaxY = direction.Y != 0 ? (stepY > 0 ? y + 1 - start.Y : start.Y - y) / Math.Abs(direction.Y) : double.MaxValue;
        var tMaxZ = direction.Z != 0 ? (stepZ > 0 ? z + 1 - start.Z : start.Z - z) / Math.Abs(direction.Z) : double.MaxValue;

        var tDeltaX = direction.X != 0 ? Math.Abs(1.0 / direction.X) : double.MaxValue;
        var tDeltaY = direction.Y != 0 ? Math.Abs(1.0 / direction.Y) : double.MaxValue;
        var tDeltaZ = direction.Z != 0 ? Math.Abs(1.0 / direction.Z) : double.MaxValue;

        var lastT = 0d;

        var startBlock = GetBlockAt(x, y, z);
        if (startBlock is { IsAir: false, IsLiquid: false })
        {
            return new RaycastHit
            {
                Block = startBlock,
                Face = BlockFace.Top,
                InsideBlock = true,
                BlockPosition = new Vector3<int>(x, y, z),
                ExactHitPosition = start,
                Distance = 0
            };
        }

        var maxSteps = Math.Max(10, (int)(maxDistance * 3));
        var steps = 0;

        while (steps < maxSteps)
        {
            if (lastT > maxDistance)
                return null;

            BlockFace lastFace;
            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                x += stepX;
                lastT = tMaxX;
                tMaxX += tDeltaX;
                lastFace = stepX > 0 ? BlockFace.West : BlockFace.East;
            }
            else if (tMaxY < tMaxZ)
            {
                y += stepY;
                lastT = tMaxY;
                tMaxY += tDeltaY;
                lastFace = stepY > 0 ? BlockFace.Bottom : BlockFace.Top;
            }
            else
            {
                z += stepZ;
                lastT = tMaxZ;
                tMaxZ += tDeltaZ;
                lastFace = stepZ > 0 ? BlockFace.North : BlockFace.South;
            }

            var block = GetBlockAt(x, y, z);
            if (block is { IsAir: false, IsLiquid: false })
            {
                var exactHitPosition = new Vector3<double>(
                    start.X + direction.X * lastT,
                    start.Y + direction.Y * lastT,
                    start.Z + direction.Z * lastT
                );

                return new RaycastHit
                {
                    Block = block,
                    Face = lastFace,
                    BlockPosition = new Vector3<int>(x, y, z),
                    ExactHitPosition = exactHitPosition,
                    Distance = lastT
                };
            }

            steps++;
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
