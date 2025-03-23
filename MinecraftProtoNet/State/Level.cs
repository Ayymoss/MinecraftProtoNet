using System.Collections.Concurrent;
using System.Diagnostics;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Handlers.Meta;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;

namespace MinecraftProtoNet.State;

public class Level
{
    public double TickInterval { get; private set; } = 50d;
    public long WorldAge { get; private set; }
    public long TimeOfDay { get; private set; }
    public bool TimeOfDayIncreasing { get; private set; }
    public Stopwatch TimeSinceLastTimePacket { get; } = new();

    public void UpdateTickInformation(long worldAge, long timeOfDay, bool timeOfDayIncreasing)
    {
        var previousWorldAge = WorldAge;

        WorldAge = worldAge;
        TimeOfDay = timeOfDay;
        TimeOfDayIncreasing = timeOfDayIncreasing;

        if (previousWorldAge > 0)
        {
            var ticksPassed = WorldAge - previousWorldAge;
            if (ticksPassed <= 0) return;

            var realTimeElapsed = TimeSinceLastTimePacket.ElapsedMilliseconds;
            TimeSinceLastTimePacket.Restart();
            var calculatedTickInterval = (double)realTimeElapsed / ticksPassed;
            const double smoothingFactor = 0.25;
            TickInterval = TickInterval * (1 - smoothingFactor) + calculatedTickInterval * smoothingFactor;
        }
        else
        {
            TimeSinceLastTimePacket.Restart();
        }
    }

    public double GetCurrentServerTps()
    {
        return 1000.0 / TickInterval;
    }

    public double GetTickRateMultiplier()
    {
        return 50.0 / TickInterval;
    }

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
    public async Task<Player> AddEntityAsync(Guid uuid, int entityId, Vector3<double>? position = null, Vector2<float>? yawPitch = null)
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
    /// Sets an entity's absolute position
    /// </summary>
    /// <param name="entityId">The entity ID to update</param>
    /// <param name="position">The new absolute position</param>
    /// <param name="onGround">Whether the entity is on the ground</param>
    public async Task SetPositionAsync(int entityId, Vector3<double> position, bool onGround)
    {
        try
        {
            await _semaphore.WaitAsync();

            var entity = GetEntityOfId(entityId);
            if (entity == null) return;

            var oldPosition = entity.Position;
            entity.Position = position;
            entity.IsOnGround = onGround;

            LogPositionChange(entityId, "SYNC", oldPosition, position);
        }
        finally
        {
            if (_semaphore.CurrentCount is 0) _semaphore.Release();
        }
    }

    /// <summary>
    /// Updates an entity's position by applying delta movements
    /// </summary>
    /// <param name="entityId">The entity ID to update</param>
    /// <param name="delta">The position delta to apply</param>
    /// <param name="onGround">Whether the entity is on the ground</param>
    public async Task UpdatePositionAsync(int entityId, Vector3<double> delta, bool onGround)
    {
        try
        {
            await _semaphore.WaitAsync();

            var entity = GetEntityOfId(entityId);
            if (entity is null) return;

            var oldPosition = entity.Position;
            entity.Position += delta;
            entity.IsOnGround = onGround;

            LogPositionChange(entityId, "DELTA", oldPosition, entity.Position);
        }
        finally
        {
            if (_semaphore.CurrentCount is 0) _semaphore.Release();
        }
    }

    private void LogPositionChange(int entityId, string reason, Vector3<double> oldPos, Vector3<double> newPos)
    {
        var delta = new Vector3<double>(
            newPos.X - oldPos.X,
            newPos.Y - oldPos.Y,
            newPos.Z - oldPos.Z
        );

        var distance = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);

        //Console.WriteLine($"Entity {entityId} position update ({reason}):");
        //Console.WriteLine($"  From: ({oldPos.X:F6}, {oldPos.Y:F6}, {oldPos.Z:F6})");
        //Console.WriteLine($"  To:   ({newPos.X:F6}, {newPos.Y:F6}, {newPos.Z:F6})");
        //Console.WriteLine($"  Delta: ({delta.X:F6}, {delta.Y:F6}, {delta.Z:F6}) dist={distance:F6}");
    }

    /// <summary>
    /// Sets an entity's velocity vector and immediately updates position to account for the velocity
    /// </summary>
    /// <param name="entityId">The entity ID to update</param>
    /// <param name="packetVelocity">The velocity from the packet (in 1/8000 blocks per tick)</param>
    public async Task SetVelocityAsync(int entityId, Vector3<short> packetVelocity)
    {
        try
        {
            await _semaphore.WaitAsync();

            var entity = GetEntityOfId(entityId);
            if (entity == null) return;

            const double conversionFactor = 1.0 / 8000.0;
            entity.Velocity *= conversionFactor;

            var tickRateMultiplier = GetTickRateMultiplier();
            var velocity = entity.Velocity * tickRateMultiplier;
            entity.Position += velocity;
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
    /// <returns>Entity</returns>
    public Entity? GetEntityOfId(int entityId)
    {
        return _playersByEntityId.TryGetValue(entityId, out var player) ? player.Entity : null;
    }

    public ConcurrentDictionary<(int ChunkX, int ChunkZ), Chunk> Chunks { get; set; } = [];

    public BlockState? GetBlockAt(int worldX, int worldY, int worldZ)
    {
        var chunkX = worldX >> 4;
        var chunkZ = worldZ >> 4;

        if (!Chunks.TryGetValue((chunkX, chunkZ), out var chunk)) return null;

        return chunk.GetBlock(worldX, worldY, worldZ);
    }

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

    public RaycastHit? RayCast(Vector3<double> start, Vector3<double> direction, double maxDistance = 100.0)
    {
        // Normalize direction vector
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
        if (length < 1e-10) return null; // Prevent division by zero

        direction = new Vector3<double>(direction.X / length, direction.Y / length, direction.Z / length);

        // Current block position
        var x = (int)Math.Floor(start.X);
        var y = (int)Math.Floor(start.Y);
        var z = (int)Math.Floor(start.Z);

        // Direction step values
        var stepX = direction.X > 0 ? 1 : -1;
        var stepY = direction.Y > 0 ? 1 : -1;
        var stepZ = direction.Z > 0 ? 1 : -1;

        // Calculate distances to next block boundaries
        var tMaxX = direction.X != 0 ? (stepX > 0 ? x + 1 - start.X : start.X - x) / Math.Abs(direction.X) : double.MaxValue;
        var tMaxY = direction.Y != 0 ? (stepY > 0 ? y + 1 - start.Y : start.Y - y) / Math.Abs(direction.Y) : double.MaxValue;
        var tMaxZ = direction.Z != 0 ? (stepZ > 0 ? z + 1 - start.Z : start.Z - z) / Math.Abs(direction.Z) : double.MaxValue;

        // Calculate step sizes
        var tDeltaX = direction.X != 0 ? Math.Abs(1.0 / direction.X) : double.MaxValue;
        var tDeltaY = direction.Y != 0 ? Math.Abs(1.0 / direction.Y) : double.MaxValue;
        var tDeltaZ = direction.Z != 0 ? Math.Abs(1.0 / direction.Z) : double.MaxValue;

        var lastT = 0d;

        // Check if the starting point is inside a block
        var startBlock = GetBlockAt(x, y, z);
        if (startBlock is { IsAir: false, IsLiquid: false })
        {
            // We're starting inside a block, so the distance is 0
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

        // Calculate the maximum number of steps based on maxDistance
        // For small distances, ensure we take at least a few steps
        int maxSteps = Math.Max(10, (int)(maxDistance * 3));
        int steps = 0;

        while (steps < maxSteps)
        {
            // Check if we've exceeded the maximum distance
            if (lastT > maxDistance)
                return null;

            // Determine which axis to step along next
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

            // Check if the current block is solid
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
