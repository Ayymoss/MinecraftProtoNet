using System.Collections.Concurrent;
using System.Diagnostics;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Models.World.Meta;
using MinecraftProtoNet.Core.Physics.Shapes;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// Represents the game world level, delegating to specialized managers.
/// </summary>
public class Level(ITickManager tickManager, IPlayerRegistry playerRegistry, IChunkManager chunkManager)
{
    /// <summary>
    /// Dimension type properties including vertical bounds (minY, height).
    /// Equivalent to Java's Level.dimensionType().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:98-100
    /// </summary>
    public DimensionType DimensionType { get; set; } = new(); // Default: -64, 384 (1.18+)

    /// <summary>
    /// World border with bounds checking.
    /// Equivalent to Java's Level.getWorldBorder().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/BetterWorldBorder.java
    /// </summary>
    public WorldBorder WorldBorder { get; set; } = new(); // Default: infinite border
    
    public event Action? OnPlayersChanged
    {
        add => playerRegistry.OnPlayersChanged += value;
        remove => playerRegistry.OnPlayersChanged -= value;
    }

    public Level() : this(new TickManager(), new PlayerRegistry(), new ChunkManager())
    {
    }

    // ==== Tick Manager Delegation ====
    
    public double TickInterval => tickManager.TickInterval;
    public long ClientTickCounter => tickManager.ClientTickCounter;
    public long WorldAge => tickManager.WorldAge;
    public long TimeOfDay => tickManager.TimeOfDay;
    public bool TimeOfDayIncreasing => tickManager.TimeOfDayIncreasing;
    public Stopwatch TimeSinceLastTimePacket => tickManager.TimeSinceLastTimePacket;

    public void UpdateTickInformation(long serverWorldAge, long timeOfDay, bool timeOfDayIncreasing)
        => tickManager.UpdateTickInformation(serverWorldAge, timeOfDay, timeOfDayIncreasing);

    public void IncrementClientTickCounter()
        => tickManager.IncrementClientTickCounter();

    public double GetCurrentServerTps()
        => tickManager.GetCurrentServerTps();

    // ==== Player Registry Delegation ====

    public Task<Player> AddPlayerAsync(Guid uuid, string username)
        => playerRegistry.AddPlayerAsync(uuid, username);

    public Task<Player> AddEntityAsync(Guid uuid, int entityId, Vector3<double>? position = null, Vector2<float>? yawPitch = null)
        => playerRegistry.AddEntityAsync(uuid, entityId, position, yawPitch);

    public Task<bool> RemovePlayerAsync(Guid uuid)
        => playerRegistry.RemovePlayerAsync(uuid);

    public Task<bool> RemoveEntityAsync(int entityId)
        => playerRegistry.RemoveEntityAsync(entityId);

    public Task SetPositionAsync(int entityId, Vector3<double> position, Vector3<double> velocity, Vector2<float> yawPitch, bool onGround)
        => playerRegistry.SetPositionAsync(entityId, position, velocity, yawPitch, onGround);

    public Task UpdatePositionAsync(int entityId, Vector3<double> delta, bool onGround)
        => playerRegistry.UpdatePositionAsync(entityId, delta, onGround);

    public Task SetVelocityAsync(int entityId, Vector3<short> packetVelocity)
        => playerRegistry.SetVelocityAsync(entityId, packetVelocity, tickManager);

    public Player? GetPlayerByUuid(Guid uuid)
        => playerRegistry.GetPlayerByUuid(uuid);

    public Player? GetPlayerByEntityId(int entityId)
        => playerRegistry.GetPlayerByEntityId(entityId);

    public Player? GetPlayerByUsername(string username)
        => playerRegistry.GetPlayerByUsername(username);

    public IReadOnlyCollection<Player> GetAllPlayers()
        => playerRegistry.GetAllPlayers();

    public int[] GetAllEntityIds()
        => playerRegistry.GetAllEntityIds();

    public IEnumerable<Player> GetAllRegisteredPlayers()
        => playerRegistry.GetAllRegisteredPlayers();

    public Entity? GetEntityOfId(int entityId)
        => playerRegistry.GetEntityOfId(entityId);

    /// <summary>
    /// Gets all player entities in the world for rendering/iteration.
    /// Equivalent to Java's ClientLevel.entitiesForRendering() (for player entities).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerContext.java:50-51
    /// Used by Baritone for entity iteration.
    /// Note: World entities (non-player entities) are accessed via ClientState.WorldEntities.
    /// This method only returns player entities since Level doesn't have access to WorldEntityRegistry.
    /// </summary>
    public IEnumerable<Entity> GetAllEntities()
    {
        // Return all player entities
        foreach (var player in GetAllPlayers())
        {
            if (player.Entity != null)
            {
                yield return player.Entity;
            }
        }
    }

    // ==== Chunk Manager Delegation ====

    public ConcurrentDictionary<(int ChunkX, int ChunkZ), Chunk> Chunks => chunkManager.Chunks;

    public BlockState? GetBlockAt(int worldX, int worldY, int worldZ)
        => chunkManager.GetBlockAt(worldX, worldY, worldZ);

    public void HandleBlockUpdate(Vector3<double> position, int blockStateId)
        => chunkManager.HandleBlockUpdate(position, blockStateId);

    public List<AABB> GetCollidingBlockAABBs(AABB queryBox)
        => chunkManager.GetCollidingBlockAABBs(queryBox);

    public IEnumerable<Physics.Shapes.VoxelShape> GetCollidingShapes(AABB queryBox)
        => chunkManager.GetCollidingShapes(queryBox);

    public RaycastHit? RayCast(Vector3<double> start, Vector3<double> direction, double maxDistance = 100.0)
        => chunkManager.RayCast(start, direction, maxDistance);

    public void AddChunk(Chunk chunk)
        => chunkManager.AddChunk(chunk);

    public bool HasChunk(int chunkX, int chunkZ)
        => chunkManager.HasChunk(chunkX, chunkZ);

    public Chunk? GetChunk(int chunkX, int chunkZ)
        => chunkManager.GetChunk(chunkX, chunkZ);
}
