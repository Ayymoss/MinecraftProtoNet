using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Core.Abstractions;

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
}
