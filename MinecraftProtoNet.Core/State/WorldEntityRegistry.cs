using System.Collections.Concurrent;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// One synched-data field: the value plus the serializer the server declared for it. The declared type is kept
/// because the index alone is ambiguous — the same index means different things on different entity classes —
/// so anything reading this data blind (tooling, reference dumps) needs it to interpret the value.
/// </summary>
/// <param name="TypeId">Ordinal of SetEntityDataPacket.MetadataType, or -1 if the server sent an unknown one.</param>
public readonly record struct EntityDataValue(int TypeId, object? Value);

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
    public float Health { get; set; } = 20f;
    public float MaxHealth { get; set; } = 20f;

    /// <summary>
    /// Raw synched entity data, keyed by the data-accessor index carried in SetEntityDataPacket. The vanilla
    /// client keeps this in SynchedEntityData; we retain it verbatim so consumers can read any field without
    /// Core having to model every entity subclass.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/syncher/SynchedEntityData.java
    /// </summary>
    public ConcurrentDictionary<byte, EntityDataValue> Metadata { get; } = new();

    /// <summary>Index 2, DATA_CUSTOM_NAME. The unformatted component, kept so callers can read colours.</summary>
    public NbtTag? CustomNameComponent { get; set; }

    /// <summary>Index 2 flattened to text with formatting codes stripped. Null when the entity has no name.</summary>
    public string? CustomName { get; set; }

    /// <summary>Index 3, DATA_CUSTOM_NAME_VISIBLE — whether the name renders above the entity.</summary>
    public bool CustomNameVisible { get; set; }

    /// <summary>Index 0 bit 5, FLAG_INVISIBLE. Nametag-holder armour stands are typically invisible.</summary>
    public bool IsInvisible { get; set; }
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
    /// Updates entity health.
    /// </summary>
    public void UpdateHealth(int entityId, float health)
    {
        if (_entities.TryGetValue(entityId, out var entity))
        {
            entity.Health = health;
        }
    }

    /// <summary>
    /// Updates entity max health.
    /// </summary>
    public void UpdateMaxHealth(int entityId, float maxHealth)
    {
        if (_entities.TryGetValue(entityId, out var entity))
        {
            entity.MaxHealth = maxHealth;
        }
    }

    /// <summary>
    /// Stores one synched-data field and decodes the handful that describe an entity's identity.
    /// Indices are the Entity.java data-accessor ordinals: 0 shared flags, 2 custom name, 3 custom name visible.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:4108-4115
    /// </summary>
    public void SetMetadata(int entityId, byte index, int typeId, object? value)
    {
        if (!_entities.TryGetValue(entityId, out var entity)) return;

        entity.Metadata[index] = new EntityDataValue(typeId, value);

        switch (index)
        {
            // FLAG_INVISIBLE = 5. Reference: Entity.java:259
            case 0 when value is byte sharedFlags:
                entity.IsInvisible = (sharedFlags & (1 << 5)) != 0;
                break;

            // DATA_CUSTOM_NAME is Optional<Component>: a null value means the name was cleared.
            case 2:
                entity.CustomNameComponent = value as NbtTag;
                entity.CustomName = value is NbtTag nameTag
                    ? ItemTextHelper.FormatTextComponent(nameTag) is { Length: > 0 } text ? text : null
                    : null;
                break;

            case 3 when value is bool nameVisible:
                entity.CustomNameVisible = nameVisible;
                break;
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
