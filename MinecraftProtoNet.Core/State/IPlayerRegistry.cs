using MinecraftProtoNet.Core.Models.Core;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// Interface for managing player and entity state.
/// </summary>
public interface IPlayerRegistry
{
    /// <summary>
    /// Event fired when the player list changes (add/remove).
    /// </summary>
    event Action? OnPlayersChanged;

    /// <summary>
    /// Adds a player to the registry or updates an existing player.
    /// </summary>
    Task<Player> AddPlayerAsync(Guid uuid, string username);

    /// <summary>
    /// Adds an entity to an existing player, or creates the player if they don't exist yet.
    /// </summary>
    Task<Player> AddEntityAsync(Guid uuid, int entityId, Vector3<double>? position = null, Vector2<float>? yawPitch = null);

    /// <summary>
    /// Removes a player from the registry.
    /// </summary>
    Task<bool> RemovePlayerAsync(Guid uuid);

    /// <summary>
    /// Removes an entity from a player but keeps the player in the registry.
    /// </summary>
    Task<bool> RemoveEntityAsync(int entityId);

    /// <summary>
    /// Sets an entity's absolute position.
    /// </summary>
    Task SetPositionAsync(int entityId, Vector3<double> position, Vector3<double> velocity, Vector2<float> yawPitch, bool onGround);

    /// <summary>
    /// Updates an entity's position by applying delta movements.
    /// </summary>
    Task UpdatePositionAsync(int entityId, Vector3<double> delta, bool onGround);

    /// <summary>
    /// Sets an entity's velocity vector.
    /// </summary>
    Task SetVelocityAsync(int entityId, Vector3<short> packetVelocity, ITickManager tickManager);

    /// <summary>
    /// Gets a player by UUID.
    /// </summary>
    Player? GetPlayerByUuid(Guid uuid);

    /// <summary>
    /// Gets a player by entity ID.
    /// </summary>
    Player? GetPlayerByEntityId(int entityId);

    /// <summary>
    /// Gets a player by username.
    /// </summary>
    Player? GetPlayerByUsername(string username);

    /// <summary>
    /// Gets all registered players.
    /// </summary>
    IReadOnlyCollection<Player> GetAllPlayers();

    /// <summary>
    /// Gets all player entity IDs.
    /// </summary>
    int[] GetAllEntityIds();

    /// <summary>
    /// Gets all fully registered players.
    /// </summary>
    IEnumerable<Player> GetAllRegisteredPlayers();

    /// <summary>
    /// Gets the entity of a player by entity ID.
    /// </summary>
    Entity? GetEntityOfId(int entityId);
}
