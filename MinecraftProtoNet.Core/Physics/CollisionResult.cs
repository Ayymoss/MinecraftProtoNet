using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Physics.Shapes;

namespace MinecraftProtoNet.Core.Physics;

/// <summary>
/// Result of a collision resolution.
/// </summary>
public readonly struct CollisionResult
{
    /// <summary>
    /// The actual movement delta after collision resolution.
    /// </summary>
    public Vector3<double> ActualDelta { get; init; }

    /// <summary>
    /// Whether there was a collision on the X axis.
    /// </summary>
    public bool CollidedX { get; init; }

    /// <summary>
    /// Whether there was a collision on the Y axis (floor or ceiling).
    /// </summary>
    public bool CollidedY { get; init; }

    /// <summary>
    /// Whether there was a collision on the Z axis.
    /// </summary>
    public bool CollidedZ { get; init; }

    /// <summary>
    /// Whether there was any horizontal collision (X or Z).
    /// </summary>
    public bool HorizontalCollision => CollidedX || CollidedZ;

    /// <summary>
    /// Whether the entity landed on the ground this tick.
    /// </summary>
    public bool LandedOnGround { get; init; }

    /// <summary>
    /// The final bounding box after movement.
    /// </summary>
    public AABB FinalBoundingBox { get; init; }
}
