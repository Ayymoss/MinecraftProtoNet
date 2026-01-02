using System.Collections.Concurrent;
using System.Diagnostics;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Physics.Shapes;

namespace MinecraftProtoNet.State;

/// <summary>
/// Represents the game world level, delegating to specialized managers.
/// </summary>
public class Level
{
    private readonly ITickManager _tickManager;
    private readonly IPlayerRegistry _playerRegistry;
    private readonly IChunkManager _chunkManager;
    
    public event Action? OnPlayersChanged
    {
        add => _playerRegistry.OnPlayersChanged += value;
        remove => _playerRegistry.OnPlayersChanged -= value;
    }

    public Level() : this(new TickManager(), new PlayerRegistry(), new ChunkManager())
    {
    }

    public Level(ITickManager tickManager, IPlayerRegistry playerRegistry, IChunkManager chunkManager)
    {
        _tickManager = tickManager;
        _playerRegistry = playerRegistry;
        _chunkManager = chunkManager;
    }

    // ==== Tick Manager Delegation ====
    
    public double TickInterval => _tickManager.TickInterval;
    public long ClientTickCounter => _tickManager.ClientTickCounter;
    public long WorldAge => _tickManager.WorldAge;
    public long TimeOfDay => _tickManager.TimeOfDay;
    public bool TimeOfDayIncreasing => _tickManager.TimeOfDayIncreasing;
    public Stopwatch TimeSinceLastTimePacket => _tickManager.TimeSinceLastTimePacket;

    public void UpdateTickInformation(long serverWorldAge, long timeOfDay, bool timeOfDayIncreasing)
        => _tickManager.UpdateTickInformation(serverWorldAge, timeOfDay, timeOfDayIncreasing);

    public void IncrementClientTickCounter()
        => _tickManager.IncrementClientTickCounter();

    public double GetCurrentServerTps()
        => _tickManager.GetCurrentServerTps();

    // ==== Player Registry Delegation ====

    public Task<Player> AddPlayerAsync(Guid uuid, string username)
        => _playerRegistry.AddPlayerAsync(uuid, username);

    public Task<Player> AddEntityAsync(Guid uuid, int entityId, Vector3<double>? position = null, Vector2<float>? yawPitch = null)
        => _playerRegistry.AddEntityAsync(uuid, entityId, position, yawPitch);

    public Task<bool> RemovePlayerAsync(Guid uuid)
        => _playerRegistry.RemovePlayerAsync(uuid);

    public Task<bool> RemoveEntityAsync(int entityId)
        => _playerRegistry.RemoveEntityAsync(entityId);

    public Task SetPositionAsync(int entityId, Vector3<double> position, Vector3<double> velocity, Vector2<float> yawPitch, bool onGround)
        => _playerRegistry.SetPositionAsync(entityId, position, velocity, yawPitch, onGround);

    public Task UpdatePositionAsync(int entityId, Vector3<double> delta, bool onGround)
        => _playerRegistry.UpdatePositionAsync(entityId, delta, onGround);

    public Task SetVelocityAsync(int entityId, Vector3<short> packetVelocity)
        => _playerRegistry.SetVelocityAsync(entityId, packetVelocity, _tickManager);

    public Player? GetPlayerByUuid(Guid uuid)
        => _playerRegistry.GetPlayerByUuid(uuid);

    public Player? GetPlayerByEntityId(int entityId)
        => _playerRegistry.GetPlayerByEntityId(entityId);

    public Player? GetPlayerByUsername(string username)
        => _playerRegistry.GetPlayerByUsername(username);

    public IReadOnlyCollection<Player> GetAllPlayers()
        => _playerRegistry.GetAllPlayers();

    public int[] GetAllEntityIds()
        => _playerRegistry.GetAllEntityIds();

    public IEnumerable<Player> GetAllRegisteredPlayers()
        => _playerRegistry.GetAllRegisteredPlayers();

    public Entity? GetEntityOfId(int entityId)
        => _playerRegistry.GetEntityOfId(entityId);

    // ==== Chunk Manager Delegation ====

    public ConcurrentDictionary<(int ChunkX, int ChunkZ), Chunk> Chunks => _chunkManager.Chunks;

    public BlockState? GetBlockAt(int worldX, int worldY, int worldZ)
        => _chunkManager.GetBlockAt(worldX, worldY, worldZ);

    public void HandleBlockUpdate(Vector3<double> position, int blockStateId)
        => _chunkManager.HandleBlockUpdate(position, blockStateId);

    public List<AABB> GetCollidingBlockAABBs(AABB queryBox)
        => _chunkManager.GetCollidingBlockAABBs(queryBox);

    public IEnumerable<Physics.Shapes.VoxelShape> GetCollidingShapes(AABB queryBox)
        => _chunkManager.GetCollidingShapes(queryBox);

    public RaycastHit? RayCast(Vector3<double> start, Vector3<double> direction, double maxDistance = 100.0)
        => _chunkManager.RayCast(start, direction, maxDistance);

    public void AddChunk(Chunk chunk)
        => _chunkManager.AddChunk(chunk);

    public bool HasChunk(int chunkX, int chunkZ)
        => _chunkManager.HasChunk(chunkX, chunkZ);

    public Chunk? GetChunk(int chunkX, int chunkZ)
        => _chunkManager.GetChunk(chunkX, chunkZ);
}
