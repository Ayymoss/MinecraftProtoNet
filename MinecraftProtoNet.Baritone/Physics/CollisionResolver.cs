using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Physics;
using MinecraftProtoNet.Physics.Shapes;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;
using static MinecraftProtoNet.Physics.PhysicsConstants;

namespace MinecraftProtoNet.Baritone.Physics;

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

        // Apply sneak edge prevention if on ground
        // Based on Minecraft's Entity.move() lines ~1000
        if (wasOnGround && isSneaking)
        {
            double stepHeight = DefaultStepHeight;
            double d0 = desiredDelta.X;
            double d1 = desiredDelta.Z;

            while (d0 != 0.0 && !HasAnyCollision(currentBox.Offset(d0, -stepHeight, 0.0), level))
            {
                if (d0 < 0.05 && d0 >= -0.05) d0 = 0.0;
                else if (d0 > 0.0) d0 -= 0.05;
                else d0 += 0.05;
            }

            while (d1 != 0.0 && !HasAnyCollision(currentBox.Offset(0.0, -stepHeight, d1), level))
            {
                if (d1 < 0.05 && d1 >= -0.05) d1 = 0.0;
                else if (d1 > 0.0) d1 -= 0.05;
                else d1 += 0.05;
            }

            while (d0 != 0.0 && d1 != 0.0 && !HasAnyCollision(currentBox.Offset(d0, -stepHeight, d1), level))
            {
                if (d0 < 0.05 && d0 >= -0.05) d0 = 0.0;
                else if (d0 > 0.0) d0 -= 0.05;
                else d0 += 0.05;
                
                if (d1 < 0.05 && d1 >= -0.05) d1 = 0.0;
                else if (d1 > 0.0) d1 -= 0.05;
                else d1 += 0.05;
            }

            desiredDelta = new Vector3<double>(d0, desiredDelta.Y, d1);
            originalDelta = desiredDelta;
        }

        // Get potential collider shapes for the entire movement path (directional expansion)
        var expandedBox = currentBox.ExpandTowards(desiredDelta.X, desiredDelta.Y, desiredDelta.Z).Expand(Epsilon);

        // We fetch shapes instead of AABBs
        // ToList() because we iterate multiple times (resolve axes, logging, step up)
        var shapes = level.GetCollidingShapes(expandedBox).ToList();

        if (shapes.Count > 0)
        {
            // _logger.LogTrace("Found {Count} colliders for move path. ExpandedBox={Box}", shapes.Count, expandedBox);
        }

        // We pass null for legacy AABB list as we use shapes overload/param
        actualDelta = ResolveMovement(originalDelta, boundingBox, null, shapes);
        currentBox = boundingBox.Offset(actualDelta);

        var collidedX = Math.Abs(actualDelta.X - originalDelta.X) > Epsilon;
        var collidedY = Math.Abs(actualDelta.Y - originalDelta.Y) > Epsilon;
        var collidedZ = Math.Abs(actualDelta.Z - originalDelta.Z) > Epsilon;
        landedOnGround = collidedY && originalDelta.Y < 0;

        // Try step-up if we collided horizontally while on ground OR in fluid
        var horizontalCollision = collidedX || collidedZ;
        if (DefaultStepHeight > 0 && (landedOnGround || wasOnGround || isInFluid) && horizontalCollision && !isSneaking)
        {
            // 1. Rise StepHeight
            var upBox = boundingBox.Offset(0, DefaultStepHeight, 0);
            // 2. Move Horizontal (using the original delta)
            // We only care about X/Z here.
            var stepDelta = ResolveMovement(new Vector3<double>(originalDelta.X, 0, originalDelta.Z), upBox, null, shapes);
            var horizBox = upBox.Offset(stepDelta.X, 0, stepDelta.Z);

            // 3. Drop StepHeight (search down)
            var dropY = ResolveMovement(new Vector3<double>(0, -DefaultStepHeight, 0), horizBox, null, shapes).Y;
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
            _logger.LogDebug("[Collision] Horizontal collision at ({X:F2}, {Y:F2}, {Z:F2}). Collider Shapes: {Count}",
                currentBox.Min.X, currentBox.Min.Y, currentBox.Min.Z, shapes.Count);
            foreach (var s in shapes)
            {
                // _logger.LogTrace("  Collider Shape: {Bounds}", s.Bounds());
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
    /// <summary>
    /// Resolves movement using VoxelShape collision logic.
    /// Replaces legacy AABB iteration with Shapes.Collide.
    /// </summary>
    private static Vector3<double> ResolveMovement(
        Vector3<double> delta,
        AABB startBox,
        List<AABB>? legacyColliders, /* Kept to satisfy method signature if called by step logic using legacy list,
                                     * BUT we need Shapes here.
                                     * Refactoring: I should pass Level or Shapes list to this method.
                                     * Legacy `ResolveMovement` took List<AABB>.
                                     * I will overload or change call sites.
                                     * Actually, MoveWithCollisions calls this.
                                     * I will change MoveWithCollisions to fetch shapes and pass them to a new ResolveMovement or inline it.
                                     */
        IEnumerable<VoxelShape> shapes)
    {
        var currentBox = startBox;

        // Resolve Y
        double yDist = delta.Y;
        if (Math.Abs(yDist) > Epsilon)
        {
            yDist = Shapes.Collide(Axis.Y, currentBox, shapes, yDist);
        }

        currentBox = currentBox.Offset(0, yDist, 0);

        // Resolve X
        double xDist = delta.X;
        if (Math.Abs(xDist) > Epsilon)
        {
            xDist = Shapes.Collide(Axis.X, currentBox, shapes, xDist);
        }

        currentBox = currentBox.Offset(xDist, 0, 0);

        // Resolve Z
        double zDist = delta.Z;
        if (Math.Abs(zDist) > Epsilon)
        {
            zDist = Shapes.Collide(Axis.Z, currentBox, shapes, zDist);
        }

        return new Vector3<double>(xDist, yDist, zDist);
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

    /// <summary>
    /// Checks if a bounding box collides with any blocks.
    /// Used by edge detection to check for ground support.
    /// </summary>
    /// <param name="box">Bounding box to check</param>
    /// <param name="level">Level to check against</param>
    /// <returns>True if there is ANY collision with blocks</returns>
    public static bool HasAnyCollision(AABB box, Level level)
    {
        var shapes = level.GetCollidingShapes(box);
        return shapes.Any(s => !s.IsEmpty());
    }
}
