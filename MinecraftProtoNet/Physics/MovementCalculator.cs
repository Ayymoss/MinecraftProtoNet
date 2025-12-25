using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;
using static MinecraftProtoNet.Physics.PhysicsConstants;

namespace MinecraftProtoNet.Physics;

/// <summary>
/// Pure calculation methods for movement physics.
/// No side effects - all methods take input and return output.
/// Based on Java's LivingEntity movement methods.
/// </summary>
public static class MovementCalculator
{
    /// <summary>
    /// Calculates the acceleration to apply based on player input.
    /// Converts local input direction to world-space acceleration using player yaw.
    /// Source: Java's moveRelative() + getFrictionInfluencedSpeed()
    /// </summary>
    /// <param name="moveX">Strafe input (-1 left, +1 right)</param>
    /// <param name="moveZ">Forward input (-1 back, +1 forward)</param>
    /// <param name="yawDegrees">Player's yaw rotation in degrees</param>
    /// <param name="speed">Movement speed (from GetFrictionInfluencedSpeed)</param>
    /// <returns>World-space acceleration vector (X, Y, Z) where Y is always 0</returns>
    public static Vector3<double> CalculateInputAcceleration(float moveX, float moveZ, float yawDegrees, float speed)
    {
        var inputLength = MathF.Sqrt(moveX * moveX + moveZ * moveZ);
        if (inputLength < 0.0001f)
        {
            return Vector3<double>.Zero;
        }

        // Normalize input if diagonal
        if (inputLength > 1f)
        {
            moveX /= inputLength;
            moveZ /= inputLength;
        }

        // Scale by speed
        moveX *= speed;
        moveZ *= speed;

        // Rotate to world space
        var yawRadians = yawDegrees * (Math.PI / 180.0);
        var sin = Math.Sin(yawRadians);
        var cos = Math.Cos(yawRadians);

        // Java: x = strafe * cos - forward * sin, z = forward * cos + strafe * sin
        var worldX = moveX * cos - moveZ * sin;
        var worldZ = moveZ * cos + moveX * sin;

        return new Vector3<double>(worldX, 0, worldZ);
    }

    /// <summary>
    /// Gets the speed multiplier based on friction and on-ground state.
    /// Source: Java's getFrictionInfluencedSpeed()
    /// </summary>
    /// <param name="blockFriction">Friction of the block below (0.6 for most blocks)</param>
    /// <param name="movementSpeed">Entity's movement speed attribute</param>
    /// <param name="onGround">Whether the entity is on the ground</param>
    /// <returns>Speed multiplier to apply to input</returns>
    public static float GetFrictionInfluencedSpeed(float blockFriction, float movementSpeed, bool onGround)
    {
        if (onGround)
        {
            // Java: speed * (0.21600002F / (friction * friction * friction))
            var frictionCubed = blockFriction * blockFriction * blockFriction;
            return movementSpeed * (0.21600002f / frictionCubed);
        }

        return DefaultFlyingSpeed; // Air control speed
    }

    /// <summary>
    /// Applies horizontal friction/drag to velocity.
    /// Source: Java's travelInAir()
    /// </summary>
    /// <param name="velocity">Current velocity</param>
    /// <param name="blockFriction">Friction of the block below</param>
    /// <param name="onGround">Whether entity is on ground</param>
    /// <returns>Velocity after friction applied</returns>
    public static Vector3<double> ApplyHorizontalFriction(Vector3<double> velocity, float blockFriction, bool onGround)
    {
        // Java: friction = blockFriction * 0.91F when on ground, 0.91F in air
        var friction = onGround ? blockFriction * AirDrag : AirDrag;

        return new Vector3<double>(
            velocity.X * friction,
            velocity.Y,
            velocity.Z * friction
        );
    }

    /// <summary>
    /// Applies gravity and vertical drag to velocity.
    /// Source: Java's travelInAir()
    /// </summary>
    /// <param name="velocity">Current velocity</param>
    /// <param name="gravity">Gravity to apply (positive value, will be subtracted)</param>
    /// <returns>Velocity after gravity applied</returns>
    public static Vector3<double> ApplyGravity(Vector3<double> velocity, double gravity)
    {
        // Java: movementY -= gravity, then *= 0.98F (vertical drag)
        var newY = (velocity.Y - gravity) * VerticalAirDrag;
        return new Vector3<double>(velocity.X, newY, velocity.Z);
    }

    /// <summary>
    /// Applies fluid-specific damping and gravity to velocity.
    /// Source: Java's travelInWater()
    /// </summary>
    public static Vector3<double> ApplyFluidPhysics(
        Vector3<double> velocity, 
        double gravity, 
        bool isSprinting, 
        bool isInWater, 
        bool isInLava)
    {
        var isFalling = velocity.Y <= 0.0;
        var slowDown = isSprinting ? 0.9f : WaterSlowdown;
        
        if (isInLava) slowDown = 0.5f;

        // Apply horizontal damping
        var dx = velocity.X * slowDown;
        var dz = velocity.Z * slowDown;

        // Apply vertical damping (0.8 for all fluids in Java)
        var dy = velocity.Y * 0.800000011920929;

        // Apply fluid-adjusted gravity
        dy = GetFluidFallingAdjustedMovement(gravity, isFalling, dy, isSprinting, isInLava);

        return new Vector3<double>(dx, dy, dz);
    }

    /// <summary>
    /// Calculates gravity adjustment in fluids.
    /// Source: Java's getFluidFallingAdjustedMovement()
    /// </summary>
    private static double GetFluidFallingAdjustedMovement(double baseGravity, bool isFalling, double dy, bool isSprinting, bool isInLava)
    {
        if (baseGravity != 0.0 && !isSprinting)
        {
            // Lava has heavier gravity penalty (gravity / 4) in Java, but call-site uses different logic.
            // In travelInWater, it uses gravity / 16.
            var gravityDivisor = isInLava ? 4.0 : 16.0;
            
            if (isFalling && Math.Abs(dy - 0.005) >= 0.003 && Math.Abs(dy - baseGravity / gravityDivisor) < 0.003)
            {
                return -0.003;
            }
            
            return dy - baseGravity / gravityDivisor;
        }

        return dy;
    }

    /// <summary>
    /// Applies a vertical push for swimming in liquid.
    /// Source: Java's jumpInLiquid()
    /// </summary>
    public static Vector3<double> ApplyFluidJump(Vector3<double> velocity)
    {
        // Java uses 0.03999999910593033
        return new Vector3<double>(velocity.X, velocity.Y + WaterJumpImpulse, velocity.Z);
    }

    /// <summary>
    /// Calculates jump velocity including sprint boost.
    /// Source: Java's jumpFromGround()
    /// </summary>
    /// <param name="currentVelocity">Current velocity</param>
    /// <param name="yawDegrees">Player's yaw for sprint direction</param>
    /// <param name="isSprinting">Whether player is sprinting</param>
    /// <param name="jumpPower">Jump power (default 0.42)</param>
    /// <param name="jumpFactor">Block's jump factor (e.g., honey = 0.5)</param>
    /// <returns>New velocity with jump applied</returns>
    public static Vector3<double> ApplyJump(Vector3<double> currentVelocity, float yawDegrees, bool isSprinting, float jumpPower = BaseJumpPower, float jumpFactor = 1.0f)
    {
        // Apply block jump factor (honey reduces jump)
        var adjustedJumpPower = jumpPower * jumpFactor;
        
        // Set vertical velocity to jump power (take max in case already moving up)
        var newY = Math.Max(adjustedJumpPower, currentVelocity.Y);
        var newX = currentVelocity.X;
        var newZ = currentVelocity.Z;

        // Apply sprint jump boost
        if (isSprinting)
        {
            var yawRadians = yawDegrees * (Math.PI / 180.0);
            // Java: -sin(yaw) * 0.2, cos(yaw) * 0.2
            newX += -Math.Sin(yawRadians) * SprintJumpBoost;
            newZ += Math.Cos(yawRadians) * SprintJumpBoost;
        }

        return new Vector3<double>(newX, newY, newZ);
    }

    /// <summary>
    /// Handles ladder/climbing movement.
    /// Source: Java's handleOnClimbable()
    /// </summary>
    /// <param name="velocity">Current velocity</param>
    /// <param name="isOnClimbable">Whether entity is on a climbable block</param>
    /// <param name="isSneaking">Whether entity is sneaking (prevents sliding down)</param>
    /// <returns>Velocity clamped for climbing</returns>
    public static Vector3<double> HandleClimbing(Vector3<double> velocity, bool isOnClimbable, bool isSneaking)
    {
        if (!isOnClimbable)
        {
            return velocity;
        }

        // Clamp horizontal movement
        var clampedX = Math.Clamp(velocity.X, -MaxClimbSpeed, MaxClimbSpeed);
        var clampedZ = Math.Clamp(velocity.Z, -MaxClimbSpeed, MaxClimbSpeed);

        // Clamp downward movement, allow upward
        var clampedY = Math.Max(velocity.Y, -MaxClimbSpeed);

        // If sneaking, prevent sliding down
        if (clampedY < 0 && isSneaking)
        {
            clampedY = 0;
        }

        return new Vector3<double>(clampedX, clampedY, clampedZ);
    }

    /// <summary>
    /// Gets the friction value for the block at the given position.
    /// Uses BlockState.Friction property set from BlockPhysicsData registry.
    /// Source: Java's Block.getFriction()
    /// </summary>
    /// <param name="level">The level to query</param>
    /// <param name="position">Position below the entity's feet</param>
    /// <returns>Block friction value</returns>
    public static float GetBlockFriction(Level level, Vector3<double> position)
    {
        var blockX = (int)Math.Floor(position.X);
        var blockY = (int)Math.Floor(position.Y - 0.5); // Check block below
        var blockZ = (int)Math.Floor(position.Z);

        var block = level.GetBlockAt(blockX, blockY, blockZ);
        return block?.Friction ?? DefaultBlockFriction;
    }

    /// <summary>
    /// Gets the speed factor for the block at the given position.
    /// Soul sand = 0.4, Honey = 0.4, default = 1.0
    /// Source: Java's getBlockSpeedFactor()
    /// </summary>
    public static float GetBlockSpeedFactor(Level level, Vector3<double> position)
    {
        var blockX = (int)Math.Floor(position.X);
        var blockY = (int)Math.Floor(position.Y - 0.5);
        var blockZ = (int)Math.Floor(position.Z);

        var block = level.GetBlockAt(blockX, blockY, blockZ);
        return block?.SpeedFactor ?? 1.0f;
    }

    /// <summary>
    /// Gets the jump factor for the block at the given position.
    /// Honey = 0.5, default = 1.0
    /// Source: Java's getBlockJumpFactor()
    /// </summary>
    public static float GetBlockJumpFactor(Level level, Vector3<double> position)
    {
        var blockX = (int)Math.Floor(position.X);
        var blockY = (int)Math.Floor(position.Y - 0.5);
        var blockZ = (int)Math.Floor(position.Z);

        var block = level.GetBlockAt(blockX, blockY, blockZ);
        return block?.JumpFactor ?? 1.0f;
    }

    /// <summary>
    /// Calculates the effective movement speed including sprint modifier.
    /// </summary>
    /// <param name="baseSpeed">Base movement speed attribute</param>
    /// <param name="isSprinting">Whether sprinting</param>
    /// <param name="isSneaking">Whether sneaking</param>
    /// <returns>Effective movement speed</returns>
    public static float GetEffectiveSpeed(double baseSpeed, bool isSprinting, bool isSneaking)
    {
        var speed = baseSpeed;

        if (isSprinting)
        {
            speed *= 1.0 + SprintSpeedModifier; // +30%
        }
        else if (isSneaking)
        {
            speed *= SneakingSpeedMultiplier; // 30%
        }

        return (float)speed;
    }

    /// <summary>
    /// Checks if movement is below minimum threshold and should be zeroed.
    /// Source: Java's MIN_MOVEMENT_DISTANCE
    /// </summary>
    public static bool IsBelowMovementThreshold(double value)
    {
        return Math.Abs(value) < MinMovementDistance;
    }

    /// <summary>
    /// Zeroes out velocity components that are below the movement threshold.
    /// </summary>
    public static Vector3<double> ClampMinimumMovement(Vector3<double> velocity)
    {
        return new Vector3<double>(
            IsBelowMovementThreshold(velocity.X) ? 0 : velocity.X,
            IsBelowMovementThreshold(velocity.Y) ? 0 : velocity.Y,
            IsBelowMovementThreshold(velocity.Z) ? 0 : velocity.Z
        );
    }
}
