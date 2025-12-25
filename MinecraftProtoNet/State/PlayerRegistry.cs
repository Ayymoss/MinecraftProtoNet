using System.Collections.Concurrent;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State.Base;

namespace MinecraftProtoNet.State;

/// <summary>
/// Manages player and entity state using thread-safe concurrent collections.
/// </summary>
public class PlayerRegistry : IPlayerRegistry
{
    private readonly ConcurrentDictionary<Guid, Player> _players = new();
    private readonly ConcurrentDictionary<int, Player> _playersByEntityId = new();

    /// <inheritdoc />
    public Task<Player> AddPlayerAsync(Guid uuid, string username)
    {
        var player = _players.GetOrAdd(uuid, _ => new Player { Uuid = uuid });
        player.Username = username;
        return Task.FromResult(player);
    }

    /// <inheritdoc />
    public Task<Player> AddEntityAsync(Guid uuid, int entityId, Vector3<double>? position = null, Vector2<float>? yawPitch = null)
    {
        // Get or create player - handles case where AddEntityPacket arrives before PlayerInfoUpdatePacket
        var player = _players.GetOrAdd(uuid, _ => new Player { Uuid = uuid });

        // Create entity if needed
        if (!player.HasEntity)
        {
            player.Entity = new Entity { EntityId = entityId };
            _playersByEntityId[entityId] = player;
        }
        else if (player.Entity.EntityId != entityId)
        {
            // Entity ID changed, update lookups
            _playersByEntityId.TryRemove(player.Entity.EntityId, out _);
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

        return Task.FromResult(player);
    }

    /// <inheritdoc />
    public Task<bool> RemovePlayerAsync(Guid uuid)
    {
        if (!_players.TryRemove(uuid, out var player)) return Task.FromResult(false);

        if (player.HasEntity)
        {
            _playersByEntityId.TryRemove(player.Entity.EntityId, out _);
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> RemoveEntityAsync(int entityId)
    {
        if (!_playersByEntityId.TryRemove(entityId, out var player)) return Task.FromResult(false);

        player.Entity = null;
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task SetPositionAsync(int entityId, Vector3<double> position, Vector3<double> velocity, Vector2<float> yawPitch, bool onGround)
    {
        var entity = GetEntityOfId(entityId);
        if (entity == null) return Task.CompletedTask;

        entity.Position = position;
        entity.Velocity = velocity;
        entity.YawPitch = yawPitch;
        entity.IsOnGround = onGround;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdatePositionAsync(int entityId, Vector3<double> delta, bool onGround)
    {
        var entity = GetEntityOfId(entityId);
        if (entity is null) return Task.CompletedTask;

        entity.Position += delta;
        entity.IsOnGround = onGround;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetVelocityAsync(int entityId, Vector3<short> packetVelocity, ITickManager tickManager)
    {
        var entity = GetEntityOfId(entityId);
        if (entity == null) return Task.CompletedTask;

        const double conversionFactor = 1.0 / 8000.0;
        var yConversionFactor = entity.IsOnGround && packetVelocity.Y < 0 ? 0 : conversionFactor;
        var velocityBlocksPerTick = new Vector3<double>(
            packetVelocity.X * conversionFactor,
            packetVelocity.Y * yConversionFactor,
            packetVelocity.Z * conversionFactor);

        entity.Velocity = velocityBlocksPerTick;

        var tickProgress = Math.Min(1.0, tickManager.TimeSinceLastTimePacket.ElapsedMilliseconds / tickManager.TickInterval);
        var positionDelta = entity.Velocity * tickProgress;

        entity.Position += positionDelta;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Player? GetPlayerByUuid(Guid uuid)
    {
        return _players.GetValueOrDefault(uuid);
    }

    /// <inheritdoc />
    public Player? GetPlayerByEntityId(int entityId)
    {
        return _playersByEntityId.GetValueOrDefault(entityId);
    }

    /// <inheritdoc />
    public Player? GetPlayerByUsername(string username)
    {
        return _players.Values.FirstOrDefault(p => p.Username == username);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Player> GetAllPlayers()
    {
        return _players.Values.ToArray();
    }

    /// <inheritdoc />
    public int[] GetAllEntityIds()
    {
        return _playersByEntityId.Keys.ToArray();
    }

    /// <inheritdoc />
    public IEnumerable<Player> GetAllRegisteredPlayers()
    {
        return _players.Values.Where(p => p.IsFullyRegistered);
    }

    /// <inheritdoc />
    public Entity? GetEntityOfId(int entityId)
    {
        return _playersByEntityId.TryGetValue(entityId, out var player) ? player.Entity : null;
    }
}
