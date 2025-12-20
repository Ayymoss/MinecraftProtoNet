using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;
using static MinecraftProtoNet.Physics.PhysicsConstants;

namespace MinecraftProtoNet.Physics;

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

/// <summary>
/// Handles collision detection and resolution.
/// Based on Java's Entity.move() and collide() methods.
/// </summary>
public static class CollisionResolver
{
    /// <summary>
    /// Moves an entity with collision detection and resolution.
    /// Source: Java's Entity.move() and Entity.collide()
    /// </summary>
    /// <param name="boundingBox">Entity's current bounding box</param>
    /// <param name="level">The level to check collisions against</param>
    /// <param name="desiredDelta">Desired movement delta</param>
    /// <param name="wasOnGround">Whether entity was on ground before movement</param>
    /// <param name="isSneaking">Whether entity is sneaking (affects step-up)</param>
    /// <returns>Collision result with actual movement and collision flags</returns>
    public static CollisionResult MoveWithCollisions(
        AABB boundingBox,
        Level level,
        Vector3<double> desiredDelta,
        bool wasOnGround,
        bool isSneaking,
        bool isInFluid = false)
    {
        var currentBox = boundingBox;
        var originalDelta = desiredDelta;
        var landedOnGround = false;

        // Early exit if no movement
        if (Math.Abs(desiredDelta.X) < Epsilon && 
            Math.Abs(desiredDelta.Y) < Epsilon && 
            Math.Abs(desiredDelta.Z) < Epsilon)
        {
            return new CollisionResult
            {
                ActualDelta = Vector3<double>.Zero,
                FinalBoundingBox = currentBox
            };
        }

        // Get potential colliders for the entire movement path
        var expandedBox = currentBox.Expand(
            desiredDelta.X > 0 ? desiredDelta.X : 0,
            desiredDelta.Y > 0 ? desiredDelta.Y : 0,
            desiredDelta.Z > 0 ? desiredDelta.Z : 0
        ).Expand(
            desiredDelta.X < 0 ? -desiredDelta.X : 0,
            desiredDelta.Y < 0 ? -desiredDelta.Y : 0,
            desiredDelta.Z < 0 ? -desiredDelta.Z : 0
        ).Expand(Epsilon);

        var colliders = level.GetCollidingBlockAABBs(expandedBox);

        // Resolve Y axis first (most important for ground detection)
        var adjustedY = desiredDelta.Y;
        foreach (var collider in colliders)
        {
            adjustedY = currentBox.CalculateYOffset(collider, adjustedY);
        }

        var collidedY = Math.Abs(adjustedY - desiredDelta.Y) > Epsilon;
        if (collidedY && desiredDelta.Y < 0)
        {
            landedOnGround = true;
        }

        currentBox = currentBox.Offset(0, adjustedY, 0);

        // Resolve X axis
        var adjustedX = desiredDelta.X;
        foreach (var collider in colliders)
        {
            adjustedX = currentBox.CalculateXOffset(collider, adjustedX);
        }

        var collidedX = Math.Abs(adjustedX - desiredDelta.X) > Epsilon;
        currentBox = currentBox.Offset(adjustedX, 0, 0);

        // Resolve Z axis
        var adjustedZ = desiredDelta.Z;
        foreach (var collider in colliders)
        {
            adjustedZ = currentBox.CalculateZOffset(collider, adjustedZ);
        }

        var collidedZ = Math.Abs(adjustedZ - desiredDelta.Z) > Epsilon;
        currentBox = currentBox.Offset(0, 0, adjustedZ);

        var actualDelta = new Vector3<double>(adjustedX, adjustedY, adjustedZ);

        // Try step-up if we collided horizontally while on ground
        // Try step-up if we collided horizontally while on ground OR in fluid
        var horizontalCollision = collidedX || collidedZ;
        if (horizontalCollision && !isSneaking && (wasOnGround || isInFluid))
        {
            var stepResult = TryStepUp(
                boundingBox,
                level,
                originalDelta.X,
                originalDelta.Z,
                colliders);

            // Use step result if it moved us further
            var stepDistSq = stepResult.X * stepResult.X + stepResult.Z * stepResult.Z;
            var normalDistSq = actualDelta.X * actualDelta.X + actualDelta.Z * actualDelta.Z;

            if (stepDistSq > normalDistSq + Epsilon)
            {
                actualDelta = stepResult;
                currentBox = boundingBox.Offset(actualDelta);
                landedOnGround = true;
                collidedX = Math.Abs(stepResult.X - originalDelta.X) > Epsilon;
                collidedZ = Math.Abs(stepResult.Z - originalDelta.Z) > Epsilon;
                collidedY = false; // We stepped up, didn't collide
            }
        }

        return new CollisionResult
        {
            ActualDelta = actualDelta,
            CollidedX = collidedX,
            CollidedY = collidedY,
            CollidedZ = collidedZ,
            LandedOnGround = landedOnGround,
            FinalBoundingBox = currentBox
        };
    }

    /// <summary>
    /// Attempts to step up over obstacles.
    /// Source: Java's Entity.collide() step-up logic
    /// </summary>
    private static Vector3<double> TryStepUp(
        AABB originalBox,
        Level level,
        double desiredDeltaX,
        double desiredDeltaZ,
        List<AABB> existingColliders)
    {
        // 1. Check if we can move up by step height
        var stepUpBox = originalBox.Offset(0, DefaultStepHeight + Epsilon, 0);
        var stepUpColliders = level.GetCollidingBlockAABBs(stepUpBox);

        // If there's something blocking us from stepping up, abort
        if (stepUpColliders.Any(c => c.Intersects(stepUpBox)))
        {
            return Vector3<double>.Zero;
        }

        // 2. Move up
        var currentY = DefaultStepHeight;
        var allColliders = level.GetCollidingBlockAABBs(
            originalBox.Offset(0, DefaultStepHeight, 0).Expand(Math.Abs(desiredDeltaX), 0, Math.Abs(desiredDeltaZ)));

        foreach (var collider in allColliders)
        {
            currentY = originalBox.CalculateYOffset(collider, currentY);
        }

        var steppedBox = originalBox.Offset(0, currentY, 0);

        // 3. Move horizontally
        var currentX = desiredDeltaX;
        foreach (var collider in allColliders)
        {
            currentX = steppedBox.CalculateXOffset(collider, currentX);
        }

        steppedBox = steppedBox.Offset(currentX, 0, 0);

        var currentZ = desiredDeltaZ;
        foreach (var collider in allColliders)
        {
            currentZ = steppedBox.CalculateZOffset(collider, currentZ);
        }

        steppedBox = steppedBox.Offset(0, 0, currentZ);

        // 4. Move back down to find ground
        var downColliders = level.GetCollidingBlockAABBs(
            steppedBox.Offset(0, -(currentY + 1.0), 0));

        var downY = -(currentY + 1.0);
        foreach (var collider in downColliders)
        {
            downY = steppedBox.CalculateYOffset(collider, downY);
        }

        // Only use step if we found solid ground
        if (Math.Abs(downY + currentY + 1.0) < DefaultStepHeight + 0.1)
        {
            // We found ground within step height
            var finalY = currentY + downY;
            if (finalY > Epsilon) // Only if we actually stepped up
            {
                return new Vector3<double>(currentX, finalY, currentZ);
            }
        }

        return Vector3<double>.Zero;
    }

    /// <summary>
    /// Resolves entity-to-entity collisions (pushing).
    /// </summary>
    public static Vector3<double> ResolveEntityCollisions(
        Entity entity,
        Level level)
    {
        var pushVelocity = Vector3<double>.Zero;
        var entityIds = level.GetAllEntityIds();

        foreach (var otherId in entityIds)
        {
            if (otherId == entity.EntityId) continue;

            var other = level.GetEntityOfId(otherId);
            if (other == null) continue;

            // Check vertical overlap
            var dy = entity.Position.Y - other.Position.Y;
            if (Math.Abs(dy) > 1.0) continue;

            // Check horizontal distance
            var dx = entity.Position.X - other.Position.X;
            var dz = entity.Position.Z - other.Position.Z;
            var distSq = dx * dx + dz * dz;

            var pushRange = PlayerWidth * 1.3;
            if (distSq >= pushRange * pushRange) continue;

            var dist = Math.Sqrt(distSq);
            if (dist < 0.01)
            {
                // Entities overlapping - push in random direction
                var angle = Random.Shared.NextDouble() * Math.PI * 2;
                dx = Math.Cos(angle);
                dz = Math.Sin(angle);
                dist = 0.01;
            }

            // Calculate push
            var pushStrength = EntityPushStrength * (1.0 - dist / pushRange);
            var pushX = Math.Clamp(dx / dist * pushStrength, -MaxEntityPushVelocity, MaxEntityPushVelocity);
            var pushZ = Math.Clamp(dz / dist * pushStrength, -MaxEntityPushVelocity, MaxEntityPushVelocity);

            pushVelocity += new Vector3<double>(pushX, 0, pushZ);
        }

        return pushVelocity;
    }
}
