using System.Collections.Concurrent;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;

namespace MinecraftProtoNet.State;

public class Level
{
    // Lookups for frequency access
    private readonly Dictionary<Guid, Player> _players = new();
    private readonly Dictionary<int, Player> _playersByEntityId = new();

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Adds a player to the registry or updates an existing player
    /// </summary>
    public async Task<Player> AddPlayerAsync(Guid uuid, string username)
    {
        try
        {
            await _semaphore.WaitAsync();

            if (!_players.TryGetValue(uuid, out var player))
            {
                player = new Player
                {
                    Uuid = uuid
                };
                _players[uuid] = player;
            }

            player.Username = username;
            return player;
        }
        finally
        {
            if (_semaphore.CurrentCount is 0) _semaphore.Release();
        }
    }

    /// <summary>
    /// Adds an entity to an existing player
    /// </summary>
    public async Task<Player> AddEntityAsync(Guid uuid, int entityId, Vector3<double>? position = null, Vector2D? yawPitch = null)
    {
        try
        {
            await _semaphore.WaitAsync();

            if (!_players.TryGetValue(uuid, out var player))
            {
                throw new InvalidOperationException($"Cannot add entity for unknown player UUID: {uuid}");
            }

            // Create or update entity
            if (!player.HasEntity)
            {
                player.Entity = new Entity
                {
                    EntityId = entityId
                };

                // Add to entity lookup
                _playersByEntityId[entityId] = player;
            }
            else if (player.Entity.EntityId != entityId)
            {
                // Entity ID changed, update lookups
                if (_playersByEntityId.ContainsKey(player.Entity.EntityId))
                {
                    _playersByEntityId.Remove(player.Entity.EntityId);
                }

                player.Entity.EntityId = entityId;
                _playersByEntityId[entityId] = player;
            }

            if (position is not null)
            {
                player.Entity.Position = position;
            }

            if (yawPitch is not null)
            {
                player.Entity.YawPitch = yawPitch;
            }

            return player;
        }
        finally
        {
            if (_semaphore.CurrentCount is 0) _semaphore.Release();
        }
    }

    /// <summary>
    /// Removes a player from the registry
    /// </summary>
    public async Task<bool> RemovePlayerAsync(Guid uuid)
    {
        try
        {
            await _semaphore.WaitAsync();

            if (!_players.TryGetValue(uuid, out var player)) return false;

            if (player.HasEntity) _playersByEntityId.Remove(player.Entity.EntityId);
            _players.Remove(uuid);
            return true;
        }
        finally
        {
            if (_semaphore.CurrentCount is 0) _semaphore.Release();
        }
    }

    /// <summary>
    /// Removes an entity from a player but keeps the player in the registry
    /// </summary>
    public async Task<bool> RemoveEntityAsync(int entityId)
    {
        try
        {
            await _semaphore.WaitAsync();

            if (!_playersByEntityId.TryGetValue(entityId, out var player)) return false;

            player.Entity = null;
            _playersByEntityId.Remove(entityId);

            return true;
        }
        finally
        {
            if (_semaphore.CurrentCount is 0) _semaphore.Release();
        }
    }

    /// <summary>
    /// Updates an entity's position and orientation
    /// </summary>
    public async Task<bool> UpdateEntityPositionAsync(int entityId, Vector3<double> position, Vector2D? yawPitch = null)
    {
        try
        {
            await _semaphore.WaitAsync();

            if (!_playersByEntityId.TryGetValue(entityId, out var player) || !player.HasEntity) return false;

            player.Entity.Position = position;
            if (yawPitch is not null) player.Entity.YawPitch = yawPitch;
            return true;
        }
        finally
        {
            if (_semaphore.CurrentCount is 0) _semaphore.Release();
        }
    }

    /// <summary>
    /// Updates an entity's velocity
    /// </summary>
    public async Task<bool> UpdateEntityVelocityAsync(int entityId, Vector3<double> velocity)
    {
        try
        {
            await _semaphore.WaitAsync();

            if (!_playersByEntityId.TryGetValue(entityId, out var player) || !player.HasEntity) return false;
            player.Entity.Velocity = velocity;
            return true;
        }
        finally
        {
            if (_semaphore.CurrentCount is 0) _semaphore.Release();
        }
    }


    /// <summary>
    /// Gets a player by UUID
    /// </summary>
    public Player? GetPlayerByUuid(Guid uuid)
    {
        return _players.GetValueOrDefault(uuid);
    }

    /// <summary>
    /// Gets a player by entity ID
    /// </summary>
    public Player? GetPlayerByEntityId(int entityId)
    {
        return _playersByEntityId.GetValueOrDefault(entityId);
    }

    /// <summary>
    /// Gets a player by username
    /// </summary>
    public Player? GetPlayerByUsername(string username)
    {
        return _players.Values.FirstOrDefault(p => p.Username == username);
    }

    /// <summary>
    /// Gets all registered players
    /// </summary>
    public IReadOnlyCollection<Player> GetAllPlayers()
    {
        return _players.Values;
    }

    /// <summary>
    /// Gets all player entity IDs
    /// </summary>
    public int[] GetAllEntityIds()
    {
        return _playersByEntityId.Keys.ToArray();
    }

    /// <summary>
    /// Gets all fully registered players
    /// </summary>
    public IEnumerable<Player> GetAllRegisteredPlayers()
    {
        return _players.Values.Where(p => p.IsFullyRegistered);
    }

    /// <summary>
    /// Gets the entity of a player by entity ID
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public Entity? GetEntityOfId(int entityId)
    {
        return _playersByEntityId.TryGetValue(entityId, out var player) ? player.Entity : null;
    }

    public ConcurrentDictionary<(int ChunkX, int ChunkZ), Chunk> Chunks { get; set; } = [];

    public BlockState? GetBlockAt(int worldX, int worldY, int worldZ)
    {
        var chunkX = (int)Math.Floor((double)worldX / 16);
        var chunkZ = (int)Math.Floor((double)worldZ / 16);

        if (!Chunks.TryGetValue((chunkX, chunkZ), out var chunk)) return null;

        return chunk.GetBlock(worldX, worldY, worldZ);
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
