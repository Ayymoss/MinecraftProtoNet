using MinecraftProtoNet.Baritone.Physics;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Physics;
using MinecraftProtoNet.State;
using static MinecraftProtoNet.Physics.PhysicsConstants;

namespace MinecraftProtoNet.Baritone.Tests.Infrastructure;

/// <summary>
/// Lightweight physics simulator for integration tests.
/// Simulates entity movement, gravity, friction, and collisions without packet sending.
/// Based on Minecraft's LivingEntity.travel() and Entity.move().
/// </summary>
public class TestPhysicsSimulator
{
    /// <summary>
    /// Maximum number of ticks to wait for entity to land.
    /// </summary>
    public int MaxLandingTicks { get; set; } = 100;

    /// <summary>
    /// Performs a single physics tick for the entity.
    /// </summary>
    public void Tick(Entity entity, Level level)
    {
        // === 1. Process Input ===
        var input = entity.Input;
        var forwardImpulse = (input.Forward ? 1.0 : 0.0) - (input.Backward ? 1.0 : 0.0);
        var strafeImpulse = (input.Left ? 1.0 : 0.0) - (input.Right ? 1.0 : 0.0);

        // === 2. Calculate Movement Speed ===
        var speed = BaseMovementSpeed;
        if (input.Sprint && forwardImpulse > 0 && !input.Shift)
        {
            speed *= (1.0 + SprintSpeedModifier);
            entity.IsSprinting = true;
        }
        else
        {
            entity.IsSprinting = false;
        }

        if (input.Shift)
        {
            speed *= SneakingSpeedMultiplier;
        }

        // === 3. Apply Movement Relative to Yaw ===
        var velocity = entity.Velocity;
        
        if (entity.IsOnGround || entity.IsInWater)
        {
            var yawRad = Math.PI / 180.0 * entity.YawPitch.X;
            var sinYaw = Math.Sin(yawRad);
            var cosYaw = Math.Cos(yawRad);

            var moveX = (strafeImpulse * cosYaw - forwardImpulse * sinYaw) * speed;
            var moveZ = (forwardImpulse * cosYaw + strafeImpulse * sinYaw) * speed;

            velocity = new Vector3<double>(
                velocity.X + moveX,
                velocity.Y,
                velocity.Z + moveZ
            );
        }

        // === 4. Jump ===
        if (input.Jump && entity.IsOnGround && entity.JumpCooldown == 0)
        {
            velocity = new Vector3<double>(velocity.X, BaseJumpPower, velocity.Z);
            entity.JumpCooldown = 10;

            // Sprint jump boost
            if (entity.IsSprinting)
            {
                var yawRad = Math.PI / 180.0 * entity.YawPitch.X;
                velocity = new Vector3<double>(
                    velocity.X - Math.Sin(yawRad) * SprintJumpBoost,
                    velocity.Y,
                    velocity.Z + Math.Cos(yawRad) * SprintJumpBoost
                );
            }
        }

        // === 5. Apply Gravity ===
        if (!entity.IsOnGround && !entity.IsInWater)
        {
            velocity = new Vector3<double>(velocity.X, velocity.Y - DefaultGravity, velocity.Z);
        }

        // === 6. Move With Collisions ===
        var boundingBox = entity.GetBoundingBox();
        var result = CollisionResolver.MoveWithCollisions(
            boundingBox, 
            level, 
            velocity, 
            entity.IsOnGround, 
            input.Shift,
            entity.IsInWater);

        entity.UpdatePositionFromAABB(result.FinalBoundingBox);
        entity.IsOnGround = result.LandedOnGround || (result.CollidedY && velocity.Y < 0);
        entity.HorizontalCollision = result.CollidedX || result.CollidedZ;

        // Update velocity based on actual movement
        entity.Velocity = result.ActualDelta;

        // === 7. Apply Friction ===
        var friction = GetBlockFriction(level, entity) * AirDrag;
        entity.Velocity = new Vector3<double>(
            entity.Velocity.X * friction,
            entity.Velocity.Y * VerticalAirDrag,
            entity.Velocity.Z * friction
        );

        // Zero out tiny velocities
        if (Math.Abs(entity.Velocity.X) < MinMovementDistance)
            entity.Velocity = new Vector3<double>(0, entity.Velocity.Y, entity.Velocity.Z);
        if (Math.Abs(entity.Velocity.Z) < MinMovementDistance)
            entity.Velocity = new Vector3<double>(entity.Velocity.X, entity.Velocity.Y, 0);

        // === 8. Update Jump Cooldown ===
        if (entity.JumpCooldown > 0)
            entity.JumpCooldown--;

        // === 9. Check Fluid State ===
        UpdateFluidState(entity, level);
    }

    /// <summary>
    /// Ticks until the entity is on ground or max ticks reached.
    /// </summary>
    public int TickUntilGrounded(Entity entity, Level level, int maxTicks = -1)
    {
        if (maxTicks < 0) maxTicks = MaxLandingTicks;

        for (int i = 0; i < maxTicks; i++)
        {
            Tick(entity, level);
            if (entity.IsOnGround)
                return i + 1;
        }
        return maxTicks;
    }

    /// <summary>
    /// Gets the friction of the block the entity is standing on.
    /// </summary>
    private static float GetBlockFriction(Level level, Entity entity)
    {
        var feetPos = entity.Position;
        var floorBlock = level.GetBlockAt(
            (int)Math.Floor(feetPos.X),
            (int)Math.Floor(feetPos.Y) - 1,
            (int)Math.Floor(feetPos.Z));

        if (floorBlock == null || !entity.IsOnGround)
            return 1.0f; // Air friction

        return floorBlock.Friction;
    }

    /// <summary>
    /// Updates the entity's fluid state based on position.
    /// </summary>
    private static void UpdateFluidState(Entity entity, Level level)
    {
        var blockAtFeet = level.GetBlockAt(
            (int)Math.Floor(entity.Position.X),
            (int)Math.Floor(entity.Position.Y),
            (int)Math.Floor(entity.Position.Z));

        entity.IsInWater = blockAtFeet?.IsLiquid == true && 
                           blockAtFeet.Name.Contains("water", StringComparison.OrdinalIgnoreCase);
        entity.IsInLava = blockAtFeet?.IsLiquid == true && 
                          blockAtFeet.Name.Contains("lava", StringComparison.OrdinalIgnoreCase);
    }
}
