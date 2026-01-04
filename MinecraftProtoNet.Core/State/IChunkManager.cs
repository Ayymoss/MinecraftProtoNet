using System.Collections.Concurrent;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Models.World.Meta;
using MinecraftProtoNet.Core.Physics.Shapes;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// Interface for managing chunk and block state.
/// </summary>
public interface IChunkManager
{
    /// <summary>
    /// The chunks currently loaded.
    /// </summary>
    ConcurrentDictionary<(int ChunkX, int ChunkZ), Chunk> Chunks { get; }

    /// <summary>
    /// Gets the block state at the given world coordinates.
    /// </summary>
    BlockState? GetBlockAt(int worldX, int worldY, int worldZ);

    /// <summary>
    /// Handles a block update at the given position.
    /// </summary>
    void HandleBlockUpdate(Vector3<double> position, int blockStateId);

    /// <summary>
    /// Gets all AABBs that collide with the query box.
    /// </summary>
    List<AABB> GetCollidingBlockAABBs(AABB queryBox);

    /// <summary>
    /// Gets all VoxelShapes that collide with the query box.
    /// </summary>
    IEnumerable<VoxelShape> GetCollidingShapes(AABB queryBox);

    /// <summary>
    /// Performs a raycast from start in direction.
    /// </summary>
    RaycastHit? RayCast(Vector3<double> start, Vector3<double> direction, double maxDistance = 100.0);

    /// <summary>
    /// Adds a chunk to the manager.
    /// </summary>
    void AddChunk(Chunk chunk);

    /// <summary>
    /// Checks if a chunk is loaded.
    /// </summary>
    bool HasChunk(int chunkX, int chunkZ);

    /// <summary>
    /// Gets a chunk by coordinates.
    /// </summary>
    Chunk? GetChunk(int chunkX, int chunkZ);
}
