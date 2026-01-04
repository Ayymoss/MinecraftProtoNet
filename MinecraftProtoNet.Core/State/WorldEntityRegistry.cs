using System.Collections.Concurrent;
using MinecraftProtoNet.Core.Models.Core;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// Represents a tracked world entity (mobs, villagers, NPCs, items, etc. - NOT players).
/// </summary>
public class WorldEntity
{
    public required int EntityId { get; init; }
    public required Guid Uuid { get; init; }
    public required int EntityType { get; set; }
    public Vector3<double> Position { get; set; } = new();
    public Vector2<float> YawPitch { get; set; } = new();
    public Vector3<double> Velocity { get; set; } = new();
    public bool IsOnGround { get; set; }
}

/// <summary>
/// Registry for tracking world entities (non-player entities like villagers, mobs, NPCs).
/// </summary>
public class WorldEntityRegistry
{
    private readonly ConcurrentDictionary<int, WorldEntity> _entities = new();

    /// <summary>
    /// Adds or updates an entity.
    /// </summary>
    public WorldEntity AddEntity(int entityId, Guid uuid, int entityType, Vector3<double> position, Vector2<float> yawPitch)
    {
        var entity = new WorldEntity
        {
            EntityId = entityId,
            Uuid = uuid,
            EntityType = entityType,
            Position = position,
            YawPitch = yawPitch
        };
        
        _entities[entityId] = entity;
        return entity;
    }

    /// <summary>
    /// Removes an entity.
    /// </summary>
    public bool RemoveEntity(int entityId)
    {
        return _entities.TryRemove(entityId, out _);
    }

    /// <summary>
    /// Gets an entity by ID.
    /// </summary>
    public WorldEntity? GetEntity(int entityId)
    {
        return _entities.GetValueOrDefault(entityId);
    }

    /// <summary>
    /// Gets all tracked entities.
    /// </summary>
    public IReadOnlyCollection<WorldEntity> GetAllEntities()
    {
        return _entities.Values.ToArray();
    }

    /// <summary>
    /// Updates entity position.
    /// </summary>
    public void UpdatePosition(int entityId, Vector3<double> delta, bool onGround)
    {
        if (_entities.TryGetValue(entityId, out var entity))
        {
            entity.Position += delta;
            entity.IsOnGround = onGround;
        }
    }

    /// <summary>
    /// Sets entity position absolutely.
    /// </summary>
    public void SetPosition(int entityId, Vector3<double> position, Vector3<double> velocity, Vector2<float> yawPitch, bool onGround)
    {
        if (_entities.TryGetValue(entityId, out var entity))
        {
            entity.Position = position;
            entity.Velocity = velocity;
            entity.YawPitch = yawPitch;
            entity.IsOnGround = onGround;
        }
    }

    /// <summary>
    /// Gets all entity IDs.
    /// </summary>
    public int[] GetAllEntityIds()
    {
        return _entities.Keys.ToArray();
    }

    /// <summary>
    /// Clears all entities.
    /// </summary>
    public void Clear()
    {
        _entities.Clear();
    }
}
