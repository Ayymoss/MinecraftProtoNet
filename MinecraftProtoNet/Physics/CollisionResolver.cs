using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;
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
    private static readonly ILogger _logger = LoggingConfiguration.CreateLogger("CollisionResolver");

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
        var actualDelta = Vector3<double>.Zero;

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

        // Get potential colliders for the entire movement path (directional expansion)
        var expandedBox = currentBox.ExpandTowards(desiredDelta.X, desiredDelta.Y, desiredDelta.Z).Expand(Epsilon);

        var colliders = level.GetCollidingBlockAABBs(expandedBox);

        if (colliders.Count > 0)
        {
            _logger.LogTrace("Found {Count} colliders for move path. ExpandedBox={Box}", colliders.Count, expandedBox);
        }

        actualDelta = ResolveMovement(originalDelta, boundingBox, colliders);
        currentBox = boundingBox.Offset(actualDelta);

        var collidedX = Math.Abs(actualDelta.X - originalDelta.X) > Epsilon;
        var collidedY = Math.Abs(actualDelta.Y - originalDelta.Y) > Epsilon;
        var collidedZ = Math.Abs(actualDelta.Z - originalDelta.Z) > Epsilon;
        landedOnGround = collidedY && originalDelta.Y < 0;

        // Try step-up if we collided horizontally while on ground OR in fluid
        var horizontalCollision = collidedX || collidedZ;
        if (DefaultStepHeight > 0 && (landedOnGround || wasOnGround || isInFluid) && horizontalCollision && !isSneaking)
        {
            // If we landed on ground this tick, step-up should start from the landed position
            var groundedBox = landedOnGround ? boundingBox.Offset(0, actualDelta.Y, 0) : boundingBox;
            
            var stepDistance = -1.0;
            var stepVector = Vector3<double>.Zero;

            // 1. Rise StepHeight
            var upBox = boundingBox.Offset(0, DefaultStepHeight, 0);
            // 2. Move Horizontal (using the original delta)
            // We only care about X/Z here.
            var stepDelta = ResolveMovement(new Vector3<double>(originalDelta.X, 0, originalDelta.Z), upBox, colliders);
            var horizBox = upBox.Offset(stepDelta.X, 0, stepDelta.Z);
            
            // 3. Drop StepHeight (search down)
            var dropY = ResolveMovement(new Vector3<double>(0, -DefaultStepHeight, 0), horizBox, colliders).Y;
            var finalStepBox = horizBox.Offset(0, dropY, 0);

            // Calculate progress
            // Minecraft uses the square distance of the horizontal movement
            // + a bias if we end up higher? No, primarily horizontal.
            var stepDistSq = stepDelta.X * stepDelta.X + stepDelta.Z * stepDelta.Z;
            var normalDistSq = actualDelta.X * actualDelta.X + actualDelta.Z * actualDelta.Z;
            
            if (stepDistSq > normalDistSq)
            {
                 // We moved further horizontally by stepping!
                 // The actualDelta needs to be the strict vector from Start to End
                 // Start: boundingBox.Min
                 // End: finalStepBox.Min
                  // The actualDelta needs to be the strict vector from Start to End
                  actualDelta = finalStepBox.Min - boundingBox.Min;
                  currentBox = finalStepBox;
                  landedOnGround = true; 
                  collidedX = Math.Abs(actualDelta.X - originalDelta.X) > Epsilon;
                  collidedZ = Math.Abs(actualDelta.Z - originalDelta.Z) > Epsilon;
                  collidedY = false; // Stepping replaces the collision with a valid move
             }
        }

        if ((collidedX || collidedZ) && (landedOnGround || wasOnGround))
        {
            // Diagnostic logging for "jumping in place"
            _logger.LogDebug("[Collision] Horizontal collision at ({X:F2}, {Y:F2}, {Z:F2}). Colliders: {Count}", 
                currentBox.Min.X, currentBox.Min.Y, currentBox.Min.Z, colliders.Count);
            foreach (var c in colliders)
            {
                _logger.LogTrace("  Collider: {Box}", c);
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
    /// Resolves movement components sequentially: Y, then X, then Z.
    /// Helper to reduce code duplication between normal move and step-up.
    /// </summary>
    private static Vector3<double> ResolveMovement(
        Vector3<double> delta,
        AABB startBox,
        List<AABB> colliders)
    {
        var currentBox = startBox;

        // Resolve Y
        var yScale = delta.Y;
        foreach (var collider in colliders)
        {
            yScale = currentBox.CalculateYOffset(collider, yScale);
        }
        currentBox = currentBox.Offset(0, yScale, 0);

        // Resolve X
        var xScale = delta.X;
        foreach (var collider in colliders)
        {
            xScale = currentBox.CalculateXOffset(collider, xScale);
        }
        currentBox = currentBox.Offset(xScale, 0, 0);

        // Resolve Z
        var zScale = delta.Z;
        foreach (var collider in colliders)
        {
            zScale = currentBox.CalculateZOffset(collider, zScale);
        }

        return new Vector3<double>(xScale, yScale, zScale);
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
