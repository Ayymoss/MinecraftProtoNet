using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Physics;
using MinecraftProtoNet.Core.Physics.Shapes;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Service for handling entity physics simulation.
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java
/// </summary>
public class PhysicsService(ILogger<PhysicsService> logger) : IPhysicsService
{
    private readonly ILogger<PhysicsService> _logger = logger;
    
    // Terminal velocity in blocks/tick (Minecraft's max fall speed)
    private const double TerminalVelocity = -3.92;
    private const double PositionUpdateThreshold = 2.0E-4;
    private const int PositionReminderInterval = 20;

    public async Task PhysicsTickAsync(
        Entity entity,
        Level level,
        IPacketSender packetSender,
        Action<Entity>? prePhysicsCallback = null)
    {
        // Invoke pre-physics callback (e.g., for pathfinding input)
        prePhysicsCallback?.Invoke(entity);

        // Update fluid state
        UpdateFluidState(entity, level);
        
        // CRITICAL: Verify IsOnGround is correct by checking if there's actually a block below
        // This fixes the case where a block is broken but IsOnGround is still true
        if (entity.IsOnGround)
        {
            var blockPosBelow = GetBlockPosBelowThatAffectsMyMovement(entity, level);
            var blockStateBelow = level.GetBlockAt(blockPosBelow.X, blockPosBelow.Y, blockPosBelow.Z);
            // If there's no solid block below, entity is not on ground
            if (blockStateBelow == null || blockStateBelow.IsAir || !blockStateBelow.HasCollision)
            {
                entity.IsOnGround = false;
                // Reset Y velocity to 0 when transitioning from ground to air (start falling from rest)
                if (entity.Velocity.Y < 0.0)
                {
                    entity.Velocity = new Vector3<double>(
                        entity.Velocity.X,
                        0.0,
                        entity.Velocity.Z);
                }
            }
        }

        // Handle jump input BEFORE travel (matches Java: jump happens before travel)
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2909-2934
        if (entity.IsJumping)
        {
            if (entity.IsInWater || entity.IsInLava)
            {
                HandleJumpInFluid(entity);
            }
            else if (entity.IsOnGround && entity.JumpCooldown == 0)
            {
                JumpFromGround(entity);
                entity.JumpCooldown = 10; // noJumpDelay = 10
            }
        }
        else
        {
            // Decrement jump cooldown when not jumping
            entity.JumpCooldown = Math.Max(0, entity.JumpCooldown - 1);
        }

        // Handle entity-to-entity pushing/collisions
        PushEntities(entity, level);

        // Calculate movement based on travel method
        // Travel applies movement internally and updates velocity for next tick
        var input = ConvertInputToVector3(entity);
        Travel(entity, level, input);

        // Send position updates to server
        await SendPositionAsync(entity, packetSender);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Updates fluid state (IsInWater, IsInLava, FluidHeight) from block states.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1567-1594
    /// </summary>
    private void UpdateFluidState(Entity entity, Level level)
    {
        var blockPos = entity.BlockPosition();
        var aabb = entity.GetBoundingBox();
        
        entity.IsInWater = false;
        entity.IsInLava = false;
        entity.FluidHeight = 0.0;
        
        var minX = (int)Math.Floor(aabb.MinX);
        var maxX = (int)Math.Floor(aabb.MaxX);
        var minY = (int)Math.Floor(aabb.MinY);
        var maxY = (int)Math.Floor(aabb.MinY + 0.001);
        var minZ = (int)Math.Floor(aabb.MinZ);
        var maxZ = (int)Math.Floor(aabb.MaxZ);
        
        double maxFluidHeight = double.NegativeInfinity;
        
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    var blockState = level.GetBlockAt(x, y, z);
                    if (blockState == null) continue;
                    
                    if (blockState.Name.Contains("water", StringComparison.OrdinalIgnoreCase))
                    {
                        entity.IsInWater = true;
                        // Calculate fluid height from level property (0-8, where 8 is full)
                        double fluidHeight = 1.0;
                        if (blockState.Properties.TryGetValue("level", out var levelStr) && 
                            int.TryParse(levelStr, out var levelInt))
                        {
                            fluidHeight = levelInt == 0 ? 1.0 : (levelInt / 8.0);
                        }
                        double blockFluidHeight = y + fluidHeight;
                        maxFluidHeight = Math.Max(maxFluidHeight, blockFluidHeight);
                    }
                    else if (blockState.Name.Contains("lava", StringComparison.OrdinalIgnoreCase))
                    {
                        entity.IsInLava = true;
                        double fluidHeight = 1.0;
                        if (blockState.Properties.TryGetValue("level", out var levelStr) && 
                            int.TryParse(levelStr, out var levelInt))
                        {
                            fluidHeight = levelInt == 0 ? 1.0 : (levelInt / 8.0);
                        }
                        double blockFluidHeight = y + fluidHeight;
                        maxFluidHeight = Math.Max(maxFluidHeight, blockFluidHeight);
                    }
                }
            }
        }
        
        if (maxFluidHeight > double.NegativeInfinity)
        {
            entity.FluidHeight = Math.Max(0.0, maxFluidHeight - aabb.MinY);
        }
    }

    /// <summary>
    /// Handles entity-to-entity pushing/collisions.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:3057-3084
    /// </summary>
    private void PushEntities(Entity entity, Level level)
    {
        var boundingBox = entity.GetBoundingBox();
        var pushableEntities = level.GetPushableEntities(entity, boundingBox).ToList();
        
        if (pushableEntities.Count == 0)
        {
            return;
        }

        // Push each entity
        foreach (var otherEntity in pushableEntities)
        {
            DoPush(entity, otherEntity);
        }
    }

    /// <summary>
    /// Performs the push interaction between two entities.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1820-1850
    /// </summary>
    private void DoPush(Entity pusher, Entity pushed)
    {
        // Calculate horizontal distance vector
        double xa = pushed.Position.X - pusher.Position.X;
        double za = pushed.Position.Z - pusher.Position.Z;
        
        // Use max of absolute values for distance calculation (Mth.absMax equivalent)
        double dd = Math.Max(Math.Abs(xa), Math.Abs(za));
        
        // Minimum distance threshold (0.009999999776482582)
        const double minDistance = 0.009999999776482582;
        if (dd < minDistance)
        {
            return;
        }

        // Normalize horizontal direction
        dd = Math.Sqrt(xa * xa + za * za);
        xa /= dd;
        za /= dd;
        
        // Calculate push strength (inverse distance, clamped to max 1.0)
        double pow = 1.0 / dd;
        if (pow > 1.0)
        {
            pow = 1.0;
        }

        // Apply push strength
        xa *= pow;
        za *= pow;
        
        // Push strength constant: 0.05000000074505806 blocks/tick
        const double pushStrength = PhysicsConstants.EntityPushStrength;
        xa *= pushStrength;
        za *= pushStrength;

        // Push both entities (pusher gets opposite direction)
        // Note: In client-side, we assume all entities are pushable
        pusher.Push(-xa, 0.0, -za);
        pushed.Push(xa, 0.0, za);
    }

    /// <summary>
    /// Routes to appropriate travel method based on fluid state.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2268-2277
    /// </summary>
    private void Travel(Entity entity, Level level, Vector3<double> input)
    {
        if (entity.IsInWater || entity.IsInLava)
        {
            TravelInFluid(entity, level, input);
        }
        else
        {
            TravelInAir(entity, level, input);
        }
    }

    /// <summary>
    /// Travel in air with gravity, friction, and block friction.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2304-2330
    /// </summary>
    private void TravelInAir(Entity entity, Level level, Vector3<double> input)
    {
        // Get block friction from block below (only if on ground)
        var blockPosBelow = GetBlockPosBelowThatAffectsMyMovement(entity, level);
        var blockStateBelow = level.GetBlockAt(blockPosBelow.X, blockPosBelow.Y, blockPosBelow.Z);
        float blockFriction = entity.IsOnGround && blockStateBelow != null 
            ? blockStateBelow.Friction 
            : 1.0f;
        float friction = blockFriction * PhysicsConstants.AirDrag;
        
        // Debug: Track velocity when falling (only log when falling and not on ground to avoid spam)
        bool isFalling = !entity.IsOnGround && entity.Velocity.Y < 0;
        double velocityYBefore = entity.Velocity.Y;
        
        // Handle input-based movement with friction
        // This calls move() internally and returns the movement vector that was applied
        var movement = HandleRelativeFrictionAndCalculateMovement(entity, level, input, blockFriction);
        double movementY = movement.Y;
        
        // Apply gravity to the Y component (for next tick)
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2320
        double movementYAfterGravity = movementY - PhysicsConstants.DefaultGravity;
        
        // Apply friction multipliers to set velocity for next tick
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2326-2327
        float verticalFriction = 0.98f; // Not FlyingAnimal, so use 0.98
        double velocityYAfterFriction = movementYAfterGravity * verticalFriction;
        
        entity.Velocity = new Vector3<double>(
            movement.X * friction,
            velocityYAfterFriction,
            movement.Z * friction);
        
        // Clamp to terminal velocity (happens after friction is applied)
        if (entity.Velocity.Y < TerminalVelocity)
        {
            entity.Velocity = new Vector3<double>(
                entity.Velocity.X,
                TerminalVelocity,
                entity.Velocity.Z);
        }
        
        // Debug logging: Only log when falling and velocity changed significantly (avoid spam)
        if (isFalling && Math.Abs(entity.Velocity.Y - velocityYBefore) > 0.001)
        {
            _logger.LogDebug(
                "[Physics] TravelInAir - EntityId={EntityId}, Y={Y}, " +
                "VelBefore={VelBefore:F6}, MovementY={MovementY:F6}, " +
                "AfterGravity={AfterGravity:F6}, AfterFriction={AfterFriction:F6}, " +
                "VelAfter={VelAfter:F6}, OnGround={OnGround}",
                entity.EntityId, entity.Position.Y,
                velocityYBefore, movementY, movementYAfterGravity, velocityYAfterFriction,
                entity.Velocity.Y, entity.IsOnGround);
        }
    }

    /// <summary>
    /// Travel in fluid (water or lava).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2332-2390
    /// </summary>
    private void TravelInFluid(Entity entity, Level level, Vector3<double> input)
    {
        bool isFalling = entity.Velocity.Y <= 0.0;
        double oldY = entity.Position.Y;
        double baseGravity = PhysicsConstants.DefaultGravity;
        
        if (entity.IsInWater)
        {
            TravelInWater(entity, level, input, baseGravity, isFalling, oldY);
        }
        else if (entity.IsInLava)
        {
            TravelInLava(entity, level, input, baseGravity, isFalling, oldY);
        }
    }

    /// <summary>
    /// Travel in water.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2345-2372
    /// </summary>
    private void TravelInWater(Entity entity, Level level, Vector3<double> input, double baseGravity, bool isFalling, double oldY)
    {
        float slowDown = entity.IsSprinting ? 0.9f : PhysicsConstants.WaterSlowdown;
        float speed = PhysicsConstants.WaterAcceleration;
        
        // Move relative to input (adds input to velocity)
        MoveRelative(entity, speed, input);
        
        // Apply movement with collision
        Move(entity, level, entity.Velocity);
        
        // Get velocity after move() (may have been modified by collisions)
        var ladderMovement = entity.Velocity;
        
        // Handle ladder collision boost
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2365-2367
        if (entity.HorizontalCollision && IsOnClimbable(entity, level))
        {
            ladderMovement = new Vector3<double>(ladderMovement.X, 0.2, ladderMovement.Z);
        }
        
        // Apply water slowdown multipliers
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2369
        ladderMovement = new Vector3<double>(
            ladderMovement.X * slowDown,
            ladderMovement.Y * 0.8,
            ladderMovement.Z * slowDown);
        
        // Apply fluid falling adjustment and set velocity for next tick
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2370
        entity.Velocity = GetFluidFallingAdjustedMovement(entity, baseGravity, isFalling, ladderMovement);
        
        // Jump out of fluid if hitting horizontal obstacle
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2392-2396
        JumpOutOfFluid(entity, level, oldY);
    }

    /// <summary>
    /// Travel in lava.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2374-2390
    /// </summary>
    private void TravelInLava(Entity entity, Level level, Vector3<double> input, double baseGravity, bool isFalling, double oldY)
    {
        // Move relative to input
        MoveRelative(entity, PhysicsConstants.WaterAcceleration, input);
        
        // Apply movement with collision
        Move(entity, level, entity.Velocity);
        
        // Apply lava slowdown based on fluid height
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2377-2383
        const double fluidJumpThreshold = 0.4;
        if (entity.FluidHeight <= fluidJumpThreshold)
        {
            entity.Velocity = new Vector3<double>(
                entity.Velocity.X * 0.5,
                entity.Velocity.Y * 0.8,
                entity.Velocity.Z * 0.5);
            var movement = GetFluidFallingAdjustedMovement(entity, baseGravity, isFalling, entity.Velocity);
            entity.Velocity = movement;
        }
        else
        {
            entity.Velocity = new Vector3<double>(
                entity.Velocity.X * 0.5,
                entity.Velocity.Y * 0.5,
                entity.Velocity.Z * 0.5);
        }
        
        // Apply additional gravity
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2385-2387
        if (baseGravity != 0.0)
        {
            entity.Velocity = new Vector3<double>(
                entity.Velocity.X,
                entity.Velocity.Y - baseGravity / 4.0,
                entity.Velocity.Z);
        }
        
        // Jump out of fluid if hitting horizontal obstacle
        JumpOutOfFluid(entity, level, oldY);
    }

    /// <summary>
    /// Handles input-based movement with friction calculation.
    /// Applies movement with collision and returns the movement vector.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2505-2515
    /// </summary>
    private Vector3<double> HandleRelativeFrictionAndCalculateMovement(Entity entity, Level level, Vector3<double> input, float blockFriction)
    {
        // Debug: Track velocity when falling
        bool isFalling = !entity.IsOnGround && entity.Velocity.Y < 0;
        double velocityYBeforeMoveRelative = entity.Velocity.Y;
        
        // Get friction-influenced speed
        float speed = GetFrictionInfluencedSpeed(entity, blockFriction);
        
        // Add input movement to velocity
        MoveRelative(entity, speed, input);
        double velocityYAfterMoveRelative = entity.Velocity.Y;
        
        // Handle climbable (ladder) logic
        entity.Velocity = HandleOnClimbable(entity, entity.Velocity);
        
        // Store velocity before Move() to track what was actually applied
        var velocityBeforeMove = entity.Velocity;
        
        // Apply movement with collision (this moves the entity and may modify velocity)
        Move(entity, level, velocityBeforeMove);
        
        // Get movement vector after move() (velocity has been modified by collisions and block speed factor)
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2509
        var movement = entity.Velocity;
        
        // Handle ladder collision boost
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2510-2511
        if ((entity.HorizontalCollision || entity.IsJumping) && IsOnClimbable(entity, level))
        {
            movement = new Vector3<double>(movement.X, 0.2, movement.Z);
        }
        
        // Debug logging: Only log when falling and there's significant change
        if (isFalling && Math.Abs(movement.Y - velocityYBeforeMoveRelative) > 0.001)
        {
            _logger.LogDebug(
                "[Physics] HandleRelativeFriction - EntityId={EntityId}, " +
                "VelBefore={VelBefore:F6}, VelAfterMoveRelative={VelAfterMoveRelative:F6}, " +
                "VelBeforeMove={VelBeforeMove:F6}, MovementY={MovementY:F6}, " +
                "BlockSpeedFactor={BlockSpeedFactor:F3}",
                entity.EntityId, velocityYBeforeMoveRelative, velocityYAfterMoveRelative,
                velocityBeforeMove.Y, movement.Y, GetBlockSpeedFactor(entity, level));
        }
        
        return movement;
    }
    
    /// <summary>
    /// Gets the block speed factor for the entity's current position.
    /// </summary>
    private float GetBlockSpeedFactor(Entity entity, Level level)
    {
        var blockPos = entity.BlockPosition();
        var blockState = level.GetBlockAt(blockPos.X, blockPos.Y, blockPos.Z);
        return blockState?.SpeedFactor ?? 1.0f;
    }

    /// <summary>
    /// Gets friction-influenced speed based on block friction.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2549-2551
    /// </summary>
    private float GetFrictionInfluencedSpeed(Entity entity, float blockFriction)
    {
        if (entity.IsOnGround)
        {
            // Base movement speed (0.1) with friction formula: 0.216 / (friction^3)
            return (float)(PhysicsConstants.BaseMovementSpeed * (0.21600002f / (blockFriction * blockFriction * blockFriction)));
        }
        else
        {
            // Flying speed (0.02 for players)
            return PhysicsConstants.DefaultFlyingSpeed;
        }
    }

    /// <summary>
    /// Moves entity relative to input direction based on yaw.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1691-1706
    /// </summary>
    private void MoveRelative(Entity entity, float speed, Vector3<double> input)
    {
        var delta = GetInputVector(input, speed, entity.YawPitch.X);
        entity.Velocity = new Vector3<double>(
            entity.Velocity.X + delta.X,
            entity.Velocity.Y + delta.Y,
            entity.Velocity.Z + delta.Z);
    }

    /// <summary>
    /// Converts input vector to movement delta based on yaw rotation.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1696-1706
    /// </summary>
    private Vector3<double> GetInputVector(Vector3<double> input, float speed, float yaw)
    {
        double lengthSqr = input.LengthSquared();
        if (lengthSqr < 1.0E-7)
        {
            return Vector3<double>.Zero;
        }
        
        var movement = lengthSqr > 1.0 ? input.Normalized() * speed : input * speed;
        
        float yawRad = yaw * (MathF.PI / 180.0f);
        float sin = MathF.Sin(yawRad);
        float cos = MathF.Cos(yawRad);
        
        return new Vector3<double>(
            movement.X * cos - movement.Z * sin,
            movement.Y,
            movement.Z * cos + movement.X * sin);
    }

    /// <summary>
    /// Converts Entity.Input to Vector3 movement vector.
    /// </summary>
    private Vector3<double> ConvertInputToVector3(Entity entity)
    {
        double x = 0.0;
        double z = 0.0;
        
        if (entity.Input.Forward) z -= 1.0;
        if (entity.Input.Backward) z += 1.0;
        if (entity.Input.Left) x -= 1.0;
        if (entity.Input.Right) x += 1.0;
        
        // Normalize diagonal movement
        if (x != 0.0 && z != 0.0)
        {
            x *= 0.7071067811865476; // 1/sqrt(2)
            z *= 0.7071067811865476;
        }
        
        return new Vector3<double>(x, 0.0, z);
    }

    /// <summary>
    /// Gets block position below entity that affects movement (for friction).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1004-1006
    /// </summary>
    private Vector3<int> GetBlockPosBelowThatAffectsMyMovement(Entity entity, Level level)
    {
        var blockPos = entity.BlockPosition();
        return new Vector3<int>(blockPos.X, blockPos.Y - 1, blockPos.Z);
    }

    /// <summary>
    /// Gets fluid falling adjusted movement.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2517-2530
    /// </summary>
    private Vector3<double> GetFluidFallingAdjustedMovement(Entity entity, double baseGravity, bool isFalling, Vector3<double> movement)
    {
        // Only apply adjustment if not sprinting
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2518
        if (baseGravity != 0.0 && !entity.IsSprinting)
        {
            double yd;
            if (isFalling && Math.Abs(movement.Y - 0.005) >= 0.003 && Math.Abs(movement.Y - baseGravity / 16.0) < 0.003)
            {
                yd = -0.003;
            }
            else
            {
                yd = movement.Y - baseGravity / 16.0;
            }
            return new Vector3<double>(movement.X, yd, movement.Z);
        }
        return movement;
    }

    /// <summary>
    /// Checks if entity can move to position without collision.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:681-687
    /// </summary>
    private bool IsFree(Entity entity, Level level, Vector3<double> movement)
    {
        var aabb = entity.GetBoundingBox().Move(movement);
        return !level.GetCollidingBlockAABBs(aabb).Any();
    }

    /// <summary>
    /// Jumps from ground, applying jump power and sprint boost if applicable.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2229-2241
    /// </summary>
    private void JumpFromGround(Entity entity)
    {
        float jumpPower = PhysicsConstants.BaseJumpPower;
        if (jumpPower > 1.0E-5f)
        {
            // Set Y velocity to jump power (or current Y if higher, e.g., from knockback)
            entity.Velocity = new Vector3<double>(
                entity.Velocity.X,
                Math.Max(jumpPower, entity.Velocity.Y),
                entity.Velocity.Z);
            
            // Apply sprint jump boost
            if (entity.IsSprinting)
            {
                float yawRad = entity.YawPitch.X * (MathF.PI / 180.0f);
                double boostX = -Math.Sin(yawRad) * PhysicsConstants.SprintJumpBoost;
                double boostZ = Math.Cos(yawRad) * PhysicsConstants.SprintJumpBoost;
                entity.Push(boostX, 0.0, boostZ);
            }
        }
    }

    /// <summary>
    /// Handles jump input when in fluid (water or lava).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2910-2931
    /// </summary>
    private void HandleJumpInFluid(Entity entity)
    {
        if (entity.IsInWater)
        {
            // Jump in water
            entity.Push(0.0, PhysicsConstants.WaterJumpImpulse, 0.0);
        }
        else if (entity.IsInLava)
        {
            // Jump in lava
            entity.Push(0.0, PhysicsConstants.WaterJumpImpulse, 0.0);
        }
    }

    /// <summary>
    /// Jumps out of fluid if hitting horizontal obstacle.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2392-2396
    /// </summary>
    private void JumpOutOfFluid(Entity entity, Level level, double oldY)
    {
        if (entity.HorizontalCollision)
        {
            var testMovement = new Vector3<double>(
                entity.Velocity.X,
                0.6000000238418579 - entity.Position.Y + oldY,
                entity.Velocity.Z);
            if (IsFree(entity, level, testMovement))
            {
                entity.Velocity = new Vector3<double>(entity.Velocity.X, 0.30000001192092896, entity.Velocity.Z);
            }
        }
    }

    /// <summary>
    /// Handles velocity when on climbable (ladder/vine).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2532-2547
    /// </summary>
    private Vector3<double> HandleOnClimbable(Entity entity, Vector3<double> delta)
    {
        if (IsOnClimbable(entity, null))
        {
            // Clamp horizontal velocity to max climb speed
            const float maxClimbSpeed = 0.15f;
            double xd = Math.Clamp(delta.X, -maxClimbSpeed, maxClimbSpeed);
            double zd = Math.Clamp(delta.Z, -maxClimbSpeed, maxClimbSpeed);
            double yd = Math.Max(delta.Y, -maxClimbSpeed);
            
            // TODO: Handle scaffolding and suppress sliding down logic when implemented
            // For now, just clamp the velocity
            
            return new Vector3<double>(xd, yd, zd);
        }
        
        return delta;
    }

    /// <summary>
    /// Checks if entity is on a climbable block (ladder, vine, etc.).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:onClimbable()
    /// </summary>
    private bool IsOnClimbable(Entity entity, Level? level)
    {
        // TODO: Implement proper climbable detection when block state system supports it
        // For now, return false - this will be implemented when ladder support is added
        return false;
    }

    /// <summary>
    /// Applies movement with collision detection and step-up logic.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:737-832
    /// </summary>
    private void Move(Entity entity, Level level, Vector3<double> delta)
    {
        if (delta.LengthSquared() < 1.0E-7)
        {
            return;
        }
        
        // Collide movement
        var movement = Collide(entity, level, delta);
        var movementLengthSqr = movement.LengthSquared();
        
        if (movementLengthSqr > 1.0E-7)
        {
            // Update position
            entity.Position = new Vector3<double>(
                entity.Position.X + movement.X,
                entity.Position.Y + movement.Y,
                entity.Position.Z + movement.Z);
        }
        
        // Update collision flags and ground detection
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:784-790
        bool xCollision = Math.Abs(delta.X - movement.X) > 1.0E-7;
        bool zCollision = Math.Abs(delta.Z - movement.Z) > 1.0E-7;
        entity.HorizontalCollision = xCollision || zCollision;
        
        bool yCollision = Math.Abs(delta.Y - movement.Y) > 1.0E-7;
        bool verticalCollisionBelow = yCollision && delta.Y < 0.0;
        bool wasOnGround = entity.IsOnGround;
        entity.IsOnGround = verticalCollisionBelow;
        
        // Reset Y velocity when landing on ground (downward velocity should be zeroed)
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java
        // When entity lands, the downward velocity is effectively reset
        if (verticalCollisionBelow && entity.Velocity.Y < 0.0)
        {
            entity.Velocity = new Vector3<double>(
                entity.Velocity.X,
                0.0,
                entity.Velocity.Z);
        }
        
        // Zero horizontal velocity on collision (for next tick)
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:808-811
        if (entity.HorizontalCollision)
        {
            entity.Velocity = new Vector3<double>(
                xCollision ? 0.0 : entity.Velocity.X,
                entity.Velocity.Y,
                zCollision ? 0.0 : entity.Velocity.Z);
        }
        
        // Apply block speed factor (affects velocity for next tick)
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:827-828
        var blockPos = entity.BlockPosition();
        var blockState = level.GetBlockAt(blockPos.X, blockPos.Y, blockPos.Z);
        float blockSpeedFactor = blockState?.SpeedFactor ?? 1.0f;
        
        // Debug: Track when falling and block speed factor is applied
        bool isFalling = !entity.IsOnGround && delta.Y < 0;
        double velocityYBeforeBlockSpeed = entity.Velocity.Y;
        
        entity.Velocity = new Vector3<double>(
            entity.Velocity.X * blockSpeedFactor,
            entity.Velocity.Y, // Y is NOT affected by block speed factor
            entity.Velocity.Z * blockSpeedFactor);
        
        // Debug logging: Only log when falling and block speed factor is not 1.0 (to catch issues)
        if (isFalling && (blockSpeedFactor != 1.0f || Math.Abs(entity.Velocity.Y - velocityYBeforeBlockSpeed) > 0.0001))
        {
            _logger.LogDebug(
                "[Physics] Move - EntityId={EntityId}, Y={Y}, " +
                "DeltaY={DeltaY:F6}, MovementY={MovementY:F6}, " +
                "VelYBeforeBlockSpeed={VelYBeforeBlockSpeed:F6}, " +
                "BlockSpeedFactor={BlockSpeedFactor:F3}, VelYAfter={VelYAfter:F6}",
                entity.EntityId, entity.Position.Y, delta.Y, movement.Y,
                velocityYBeforeBlockSpeed, blockSpeedFactor, entity.Velocity.Y);
        }
    }

    /// <summary>
    /// Collides movement with blocks, returns actual movement delta.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1089-1118
    /// </summary>
    private Vector3<double> Collide(Entity entity, Level level, Vector3<double> movement)
    {
        var aabb = entity.GetBoundingBox();
        
        // Get colliding shapes
        var collidingShapes = level.GetCollidingShapes(aabb.ExpandTowards(movement)).ToList();
        
        // Collide with shapes (X, Y, Z order)
        var resolvedMovement = CollideWithShapes(movement, aabb, collidingShapes);
        
        // Check for collisions
        bool xCollision = Math.Abs(movement.X - resolvedMovement.X) > 1.0E-7;
        bool yCollision = Math.Abs(movement.Y - resolvedMovement.Y) > 1.0E-7;
        bool zCollision = Math.Abs(movement.Z - resolvedMovement.Z) > 1.0E-7;
        bool onGroundAfterCollision = yCollision && movement.Y < 0.0;
        
        // Step-up logic
        double stepHeight = PhysicsConstants.DefaultStepHeight;
        if (stepHeight > 0.0 && (onGroundAfterCollision || entity.IsOnGround) && (xCollision || zCollision))
        {
            var groundedAABB = onGroundAfterCollision ? aabb.Move(0.0, resolvedMovement.Y, 0.0) : aabb;
            var stepUpAABB = groundedAABB.ExpandTowards(movement.X, stepHeight, movement.Z);
            if (!onGroundAfterCollision)
            {
                stepUpAABB = stepUpAABB.ExpandTowards(0.0, -9.999999747378752E-6, 0.0);
            }
            
            var stepUpShapes = level.GetCollidingShapes(stepUpAABB).ToList();
            var candidateHeights = CollectCandidateStepUpHeights(groundedAABB, stepUpShapes, (float)stepHeight, (float)resolvedMovement.Y);
            
            foreach (var candidateHeight in candidateHeights)
            {
                var stepMovement = new Vector3<double>(movement.X, candidateHeight, movement.Z);
                var stepFromGround = CollideWithShapes(stepMovement, groundedAABB, stepUpShapes);
                double horizontalDistSqr = stepFromGround.X * stepFromGround.X + stepFromGround.Z * stepFromGround.Z;
                double resolvedDistSqr = resolvedMovement.X * resolvedMovement.X + resolvedMovement.Z * resolvedMovement.Z;
                
                if (horizontalDistSqr > resolvedDistSqr)
                {
                    double distanceToGround = aabb.MinY - groundedAABB.MinY;
                    return new Vector3<double>(
                        stepFromGround.X,
                        stepFromGround.Y - distanceToGround,
                        stepFromGround.Z);
                }
            }
        }
        
        return resolvedMovement;
    }

    /// <summary>
    /// Collides movement with a list of VoxelShapes.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1171-1189
    /// </summary>
    private Vector3<double> CollideWithShapes(Vector3<double> movement, AABB boundingBox, List<VoxelShape> shapes)
    {
        if (shapes.Count == 0)
        {
            return movement;
        }
        
        var resolvedMovement = Vector3<double>.Zero;
        
        // Resolve collisions in X, Y, Z order (vanilla uses axisStepOrder which optimizes, but X-Y-Z works)
        var axes = new[] { Axis.X, Axis.Y, Axis.Z };
        foreach (var axis in axes)
        {
            double axisMovement = axis switch
            {
                Axis.X => movement.X,
                Axis.Y => movement.Y,
                Axis.Z => movement.Z,
                _ => 0.0
            };
            
            if (Math.Abs(axisMovement) > 1.0E-7)
            {
                var movedBox = boundingBox.Move(resolvedMovement.X, resolvedMovement.Y, resolvedMovement.Z);
                double collision = Shapes.Collide(axis, movedBox, shapes, axisMovement);
                
                resolvedMovement = axis switch
                {
                    Axis.X => new Vector3<double>(collision, resolvedMovement.Y, resolvedMovement.Z),
                    Axis.Y => new Vector3<double>(resolvedMovement.X, collision, resolvedMovement.Z),
                    Axis.Z => new Vector3<double>(resolvedMovement.X, resolvedMovement.Y, collision),
                    _ => resolvedMovement
                };
            }
        }
        
        return resolvedMovement;
    }

    /// <summary>
    /// Collects candidate step-up heights from colliding shapes.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1120-1143
    /// </summary>
    private float[] CollectCandidateStepUpHeights(AABB boundingBox, List<VoxelShape> colliders, float maxStepHeight, float stepHeightToSkip)
    {
        var candidates = new HashSet<float>();
        
        foreach (var collider in colliders)
        {
            var coords = collider.GetCoords(Axis.Y);
            for (int i = 0; i < coords.Count; i++)
            {
                double coord = coords.GetDouble(i);
                float relativeCoord = (float)(coord - boundingBox.MinY);
                
                if (relativeCoord >= 0.0f && relativeCoord != stepHeightToSkip && relativeCoord <= maxStepHeight)
                {
                    candidates.Add(relativeCoord);
                }
            }
        }
        
        var sorted = candidates.ToArray();
        Array.Sort(sorted);
        return sorted;
    }

    /// <summary>
    /// Sends position updates to server based on changes.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:248-286
    /// </summary>
    private async Task SendPositionAsync(Entity entity, IPacketSender packetSender)
    {
        double deltaX = entity.Position.X - entity.LastSentPosition.X;
        double deltaY = entity.Position.Y - entity.LastSentPosition.Y;
        double deltaZ = entity.Position.Z - entity.LastSentPosition.Z;
        double deltaYRot = entity.YawPitch.X - entity.LastSentYawPitch.X;
        double deltaXRot = entity.YawPitch.Y - entity.LastSentYawPitch.Y;
        
        entity.PositionReminder++;
        bool move = Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ) > PositionUpdateThreshold || 
                    entity.PositionReminder >= PositionReminderInterval;
        bool rot = Math.Abs(deltaYRot) > 1.0E-7 || Math.Abs(deltaXRot) > 1.0E-7;
        
        var flags = MovementFlags.None;
        if (entity.IsOnGround) flags |= MovementFlags.OnGround;
        if (entity.HorizontalCollision) flags |= MovementFlags.HorizontalCollision;
        
        if (move && rot)
        {
            await packetSender.SendPacketAsync(new MovePlayerPositionRotationPacket
            {
                X = entity.Position.X,
                Y = entity.Position.Y,
                Z = entity.Position.Z,
                Yaw = entity.YawPitch.X,
                Pitch = entity.YawPitch.Y,
                Flags = flags
            });
        }
        else if (move)
        {
            await packetSender.SendPacketAsync(new MovePlayerPositionPacket
            {
                X = entity.Position.X,
                Y = entity.Position.Y,
                Z = entity.Position.Z,
                Flags = flags
            });
        }
        else if (rot)
        {
            await packetSender.SendPacketAsync(new MovePlayerRotationPacket
            {
                Yaw = entity.YawPitch.X,
                Pitch = entity.YawPitch.Y,
                Flags = flags
            });
        }
        else if (entity.IsOnGround != entity.LastSentOnGround || 
                 entity.HorizontalCollision != entity.LastSentHorizontalCollision)
        {
            // Status-only packet (no position/rotation change)
            // Note: Minecraft sends a status-only packet, but we don't have that packet type.
            // For now, we'll send a position packet with same position but updated flags.
            await packetSender.SendPacketAsync(new MovePlayerPositionPacket
            {
                X = entity.LastSentPosition.X,
                Y = entity.LastSentPosition.Y,
                Z = entity.LastSentPosition.Z,
                Flags = flags
            });
        }
        
        // Update last sent values
        if (move)
        {
            entity.LastSentPosition = entity.Position;
            entity.PositionReminder = 0;
        }
        
        if (rot)
        {
            entity.LastSentYawPitch = entity.YawPitch;
        }
        
        entity.LastSentOnGround = entity.IsOnGround;
        entity.LastSentHorizontalCollision = entity.HorizontalCollision;
    }

    /// <summary>
    /// Applies knockback to an entity.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:1563-1576
    /// </summary>
    /// <param name="entity">The entity to apply knockback to</param>
    /// <param name="power">Knockback power (default is 0.4)</param>
    /// <param name="xd">X direction component (from attacker to entity)</param>
    /// <param name="zd">Z direction component (from attacker to entity)</param>
    /// <param name="knockbackResistance">Knockback resistance attribute value (0.0 to 1.0)</param>
    public void Knockback(Entity entity, double power, double xd, double zd, double knockbackResistance = 0.0)
    {
        // Apply knockback resistance
        power *= 1.0 - knockbackResistance;
        
        if (power <= 0.0)
        {
            return;
        }

        // If direction is too small, use random direction
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:1569-1571
        var random = new Random();
        while (xd * xd + zd * zd < 9.999999747378752E-6)
        {
            xd = (random.NextDouble() - random.NextDouble()) * 0.01;
            zd = (random.NextDouble() - random.NextDouble()) * 0.01;
        }

        // Normalize and scale direction vector
        var direction = new Vector3<double>(xd, 0.0, zd);
        var directionLength = Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
        if (directionLength > 0.0)
        {
            direction = new Vector3<double>(
                direction.X / directionLength * power,
                0.0,
                direction.Z / directionLength * power);
        }

        // Apply knockback to velocity
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:1574
        var currentVelocity = entity.Velocity;
        double newY = entity.IsOnGround 
            ? Math.Min(0.4, currentVelocity.Y / 2.0 + power) 
            : currentVelocity.Y;
        
        entity.Velocity = new Vector3<double>(
            currentVelocity.X / 2.0 - direction.X,
            newY,
            currentVelocity.Z / 2.0 - direction.Z);
    }
}
