using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Baritone.Tests.Infrastructure;

/// <summary>
/// Test implementation of IPlayerRegistry for unit tests.
/// Player contains Entity as a property, not inheritance.
/// </summary>
public class TestPlayerRegistry : IPlayerRegistry
{
    private readonly Dictionary<Guid, Player> _playersByUuid = new();
    private readonly Dictionary<int, Player> _playersByEntityId = new();
    private readonly Dictionary<int, Entity> _entitiesByEntityId = new();

    public event Action? OnPlayersChanged;

    public Task<Player> AddPlayerAsync(Guid uuid, string username)
    {
        var player = new Player
        {
            Uuid = uuid,
            Username = username
        };
        _playersByUuid[uuid] = player;
        OnPlayersChanged?.Invoke();
        return Task.FromResult(player);
    }

    public Task<Player> AddEntityAsync(Guid uuid, int entityId, Vector3<double>? position = null, Vector2<float>? yawPitch = null)
    {
        var entity = new Entity
        {
            EntityId = entityId,
            Position = position ?? new Vector3<double>(0, 64, 0),
            YawPitch = yawPitch ?? new Vector2<float>(0, 0)
        };
        
        var player = new Player
        {
            Uuid = uuid,
            Entity = entity
        };
        
        _playersByUuid[uuid] = player;
        _playersByEntityId[entityId] = player;
        _entitiesByEntityId[entityId] = entity;
        OnPlayersChanged?.Invoke();
        return Task.FromResult(player);
    }

    public Task<bool> RemovePlayerAsync(Guid uuid)
    {
        if (_playersByUuid.TryGetValue(uuid, out var player))
        {
            _playersByUuid.Remove(uuid);
            if (player.Entity != null)
            {
                _playersByEntityId.Remove(player.Entity.EntityId);
                _entitiesByEntityId.Remove(player.Entity.EntityId);
            }
            OnPlayersChanged?.Invoke();
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> RemoveEntityAsync(int entityId)
    {
        if (_entitiesByEntityId.TryGetValue(entityId, out _))
        {
            _entitiesByEntityId.Remove(entityId);
            if (_playersByEntityId.TryGetValue(entityId, out var player))
            {
                _playersByUuid.Remove(player.Uuid);
                _playersByEntityId.Remove(entityId);
            }
            OnPlayersChanged?.Invoke();
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task SetPositionAsync(int entityId, Vector3<double> position, Vector3<double> velocity, Vector2<float> yawPitch, bool onGround)
    {
        if (_entitiesByEntityId.TryGetValue(entityId, out var entity))
        {
            entity.Position = position;
            entity.Velocity = velocity;
            entity.YawPitch = yawPitch;
            entity.IsOnGround = onGround;
        }
        return Task.CompletedTask;
    }

    public Task UpdatePositionAsync(int entityId, Vector3<double> delta, bool onGround)
    {
        if (_entitiesByEntityId.TryGetValue(entityId, out var entity))
        {
            entity.Position = new Vector3<double>(
                entity.Position.X + delta.X,
                entity.Position.Y + delta.Y,
                entity.Position.Z + delta.Z
            );
            entity.IsOnGround = onGround;
        }
        return Task.CompletedTask;
    }

    public Task SetVelocityAsync(int entityId, Vector3<short> packetVelocity, ITickManager tickManager)
    {
        if (_entitiesByEntityId.TryGetValue(entityId, out var entity))
        {
            entity.Velocity = new Vector3<double>(
                packetVelocity.X / 8000.0,
                packetVelocity.Y / 8000.0,
                packetVelocity.Z / 8000.0
            );
        }
        return Task.CompletedTask;
    }

    public Player? GetPlayerByUuid(Guid uuid) =>
        _playersByUuid.GetValueOrDefault(uuid);

    public Player? GetPlayerByEntityId(int entityId) =>
        _playersByEntityId.GetValueOrDefault(entityId);

    public Player? GetPlayerByUsername(string username) =>
        _playersByUuid.Values.FirstOrDefault(p => p.Username == username);

    public IReadOnlyCollection<Player> GetAllPlayers() => _playersByUuid.Values.ToList();

    public int[] GetAllEntityIds() => _entitiesByEntityId.Keys.ToArray();

    public IEnumerable<Player> GetAllRegisteredPlayers() => _playersByUuid.Values;

    public Entity? GetEntityOfId(int entityId) =>
        _entitiesByEntityId.GetValueOrDefault(entityId);
}
