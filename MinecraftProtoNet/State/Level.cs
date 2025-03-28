using System.Collections.Concurrent;
using System.Diagnostics;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;

namespace MinecraftProtoNet.State;

public class Level
{
    public double TickInterval { get; private set; } = 50d;
    public long ClientTickCounter { get; private set; }
    public long WorldAge { get; set; }
    public long TimeOfDay { get; private set; }
    public bool TimeOfDayIncreasing { get; private set; }
    public Stopwatch TimeSinceLastTimePacket { get; } = new();
    private readonly Lock _tickLock = new();

    public void UpdateTickInformation(long serverWorldAge, long timeOfDay, bool timeOfDayIncreasing)
    {
        lock (_tickLock)
        {
            var previousServerWorldAge = WorldAge;

            WorldAge = serverWorldAge;
            TimeOfDay = timeOfDay;
            TimeOfDayIncreasing = timeOfDayIncreasing;

            if (previousServerWorldAge > 0)
            {
                var serverTicksPassed = WorldAge - previousServerWorldAge;
                if (serverTicksPassed > 0)
                {
                    var realTimeElapsed = TimeSinceLastTimePacket.ElapsedMilliseconds;
                    TimeSinceLastTimePacket.Restart();

                    if (realTimeElapsed > 0)
                    {
                        var calculatedTickInterval = (double)realTimeElapsed / serverTicksPassed;
                        const double smoothingFactor = 0.25;
                        TickInterval = TickInterval * (1 - smoothingFactor) + calculatedTickInterval * smoothingFactor;
                        TickInterval = Math.Clamp(TickInterval, 5.0, 1000.0);
                    }
                }
                else
                {
                    TimeSinceLastTimePacket.Restart();
                }
            }
            else
            {
                TimeSinceLastTimePacket.Restart();
            }

            ClientTickCounter = serverWorldAge;
        }
    }

    public void IncrementClientTickCounter()
    {
        lock (_tickLock)
        {
            ClientTickCounter++;
        }
    }

    public double GetCurrentServerTps()
    {
        const double epsilon = 1e-9;
        return TickInterval > epsilon ? 1000.0 / TickInterval : 0.0;
    }

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
    /// <param name="velocity">The new velocity</param>
    /// <param name="yawPitch">The new yaw and pitch</param>
    /// <param name="onGround">Whether the entity is on the ground</param>
    public async Task SetPositionAsync(int entityId, Vector3<double> position, Vector3<double> velocity, Vector2<float> yawPitch,
        bool onGround)
    {
        try
        {
            await _semaphore.WaitAsync();

            var entity = GetEntityOfId(entityId);
            if (entity == null) return;

            entity.Position = position;
            entity.Velocity = velocity;
            entity.YawPitch = yawPitch;
            entity.IsOnGround = onGround;
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

            entity.Position += delta;
            entity.IsOnGround = onGround;
        }
        finally
        {
            if (_semaphore.CurrentCount is 0) _semaphore.Release();
        }
    }

    /// <summary>
    /// Sets an entity's velocity vector and updates position accordingly
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
            var velocityBlocksPerTick = new Vector3<double>(packetVelocity.X * conversionFactor, packetVelocity.Y * conversionFactor,
                packetVelocity.Z * conversionFactor);

            entity.Velocity = velocityBlocksPerTick;

            var tickProgress = Math.Min(1.0, TimeSinceLastTimePacket.ElapsedMilliseconds / TickInterval);
            var positionDelta = entity.Velocity * tickProgress;

            entity.Position += positionDelta;
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
                    if (blockState is not { IsAir: false, IsLiquid: false, IsSolid: true }) continue;

                    // I'm only supporting floor slabs for now. Everything else is a full block.
                    var blockBox = new AABB(x, y, z, x + 1, y + 1, z + 1);
                    if (blockState.Name.Contains("slab")) blockBox = new AABB(x, y, z, x + 1, y + 0.5, z + 1);

                    if (blockBox.Intersects(queryBox))
                    {
                        collidingBoxes.Add(blockBox);
                    }
                }
            }
        }

        return collidingBoxes;
    }

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
