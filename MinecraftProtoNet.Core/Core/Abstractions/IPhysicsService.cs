using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Core.Core.Abstractions;

/// <summary>
/// Service for handling entity physics simulation.
/// </summary>
public interface IPhysicsService
{
    /// <summary>
    /// Performs a physics tick for the given entity.
    /// </summary>
    /// <param name="entity">The entity to simulate</param>
    /// <param name="level">The level for collision detection</param>
    /// <param name="sendPacketAsync">Delegate to send packets to the server</param>
    /// <param name="prePhysicsCallback">Callback invoked before physics (e.g., for pathfinding input)</param>
    Task PhysicsTickAsync(
        Entity entity,
        Level level,
        IPacketSender packetSender,
        Action<Entity>? prePhysicsCallback = null);

    /// <summary>
    /// Applies knockback to an entity.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:1563-1576
    /// </summary>
    /// <param name="entity">The entity to apply knockback to</param>
    /// <param name="power">Knockback power (default is 0.4)</param>
    /// <param name="xd">X direction component (from attacker to entity)</param>
    /// <param name="zd">Z direction component (from attacker to entity)</param>
    /// <param name="knockbackResistance">Knockback resistance attribute value (0.0 to 1.0)</param>
    void Knockback(Entity entity, double power, double xd, double zd, double knockbackResistance = 0.0);
}
