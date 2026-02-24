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
        await entity.StateLock.WaitAsync();
        try
        {
            // ===== TELEPORT GUARD =====
            // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:744-753
            // When a server teleport is received, the handler already sent AcceptTeleportation + PosRot confirmation.
            // On the FIRST physics tick after that, we must NOT apply any movement/gravity.
            // The entity must remain at the exact teleported position so the server accepts our confirmation.
            // We clear the flag and skip all physics for this tick. On the NEXT tick, normal physics resume.
            if (entity.HasPendingTeleport)
            {
                entity.HasPendingTeleport = false;

                _logger.LogDebug("[Physics] Skipping tick - pending teleport. Position={Position}, Velocity={Velocity}",
                    entity.Position, entity.Velocity);

                // Still invoke the pre-physics callback so Baritone can observe the new position,
                // but we do NOT apply any movement or send position packets this tick.
                prePhysicsCallback?.Invoke(entity);

                // Send position from the teleported state (no movement applied).
                // This ensures the server sees us at the exact teleported position on the next game tick.
                await SendPositionAsync(entity, packetSender);
                return;
            }

            // Invoke pre-physics callback (e.g., for pathfinding input)
            prePhysicsCallback?.Invoke(entity);

            // Update fluid state
            UpdateFluidState(entity, level);

            // TODO: REMOVE
            var tick = level.ClientTickCounter;
            if (tick % 20 == 0)
            {
                var state = new
                {
                    entity.Forward,
                    entity.Backward,
                    entity.Left,
                    entity.Right,
                    entity.IsJumping,
                    entity.IsSprinting,
                    entity.Velocity,
                    entity.Position,
                    entity.YawPitch,
                    entity.IsOnGround,
                };

                _logger.LogInformation("PhysicsService: PhysicsTickAsync - tick={Tick}, State={@State}", tick, state);
            }

            // Handle jump input BEFORE travel (matches Java: jump happens before travel)
            // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2909-2934
            // IMPORTANT: noJumpDelay is decremented BEFORE the jump check, not in the else branch.
            // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2903-2905
            if (entity.JumpCooldown > 0)
            {
                entity.JumpCooldown--;
            }

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
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2932
                // When not jumping, reset cooldown immediately
                entity.JumpCooldown = 0;
            }

            // Handle entity-to-entity pushing/collisions
            PushEntities(entity, level);

            // Sync entity.IsSprinting from input state BEFORE travel.
            // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java
            // In Java, aiStep() determines sprint state before travel() and sendPosition().
            // entity.IsSprinting drives speed calculations in GetFrictionInfluencedSpeed and JumpFromGround.
            entity.IsSprinting = entity.InputState.Current.Sprint;

            // Calculate movement based on travel method
            // Travel applies movement internally and updates velocity for next tick
            var input = ConvertInputToVector3(entity);
            Travel(entity, level, input);

            // Send position updates to server
            await SendPositionAsync(entity, packetSender);
        }
        finally
        {
            entity.StateLock.Release();
        }
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
    private Vector3<double> HandleRelativeFrictionAndCalculateMovement(Entity entity, Level level, Vector3<double> input,
        float blockFriction)
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
        var velBeforeClimbable = entity.Velocity;
        entity.Velocity = HandleOnClimbable(entity, level, entity.Velocity);
        bool onClimbable = IsOnClimbable(entity, level);

        // Store velocity before Move() to track what was actually applied
        var velocityBeforeMove = entity.Velocity;
        var posBeforeMove = entity.Position;
        bool onGroundBeforeMove = entity.IsOnGround;

        // Apply movement with collision (this moves the entity and may modify velocity)
        Move(entity, level, velocityBeforeMove);

        // Get movement vector after move() (velocity has been modified by collisions and block speed factor)
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2509
        var movement = entity.Velocity;

        // Handle ladder collision boost
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2510-2511
        if ((entity.HorizontalCollision || entity.IsJumping) && IsOnClimbable(entity, level))
        {
            movement = new Vector3<double>(movement.X, PhysicsConstants.ClimbUpSpeed, movement.Z);
        }

        // Debug logging: Only log when falling and there's significant change
        if (!onClimbable && isFalling && Math.Abs(movement.Y - velocityYBeforeMoveRelative) > 0.001)
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
    /// In Java, getSpeed() returns the MOVEMENT_SPEED attribute value which includes modifiers.
    /// Sprint modifier is +30% via ADD_MULTIPLIED_TOTAL (LivingEntity.java:3866).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/player/Player.java:442
    ///   this.setSpeed((float)this.getAttributeValue(Attributes.MOVEMENT_SPEED));
    /// </summary>
    private float GetFrictionInfluencedSpeed(Entity entity, float blockFriction)
    {
        if (entity.IsOnGround)
        {
            // Calculate effective movement speed with attribute modifiers
            // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2162-2168
            // Sprint modifier: ADD_MULTIPLIED_TOTAL with value 0.3 → speed * (1 + 0.3) = speed * 1.3
            double effectiveSpeed = PhysicsConstants.BaseMovementSpeed;
            if (entity.IsSprinting)
            {
                effectiveSpeed *= (1.0 + PhysicsConstants.SprintSpeedModifier);
            }

            return (float)(effectiveSpeed * (0.21600002f / (blockFriction * blockFriction * blockFriction)));
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

        // Rotation matrix matches Java implementation exactly
        return new Vector3<double>(
            movement.X * cos - movement.Z * sin,
            movement.Y,
            movement.Z * cos + movement.X * sin);
    }

    /// <summary>
    /// Converts Entity.Input to Vector3 movement vector.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:626-657
    /// Java: modifyInput() scales by 0.98, then by SNEAKING_SPEED (0.3) if crouching,
    ///       then modifyInputSpeedForSquareMovement clamps magnitude to [0,1].
    /// </summary>
    private Vector3<double> ConvertInputToVector3(Entity entity)
    {
        double x = 0.0;
        double z = 0.0;

        // CRITICAL FIX: Forward = positive Z (South), Backward = negative Z (North)
        // This matches Input.GetMoveVector() and Minecraft's coordinate system
        if (entity.Input.Forward) z += 1.0;   // Fixed: was z -= 1.0
        if (entity.Input.Backward) z -= 1.0;   // Fixed: was z += 1.0
        if (entity.Input.Left) x -= 1.0;
        if (entity.Input.Right) x += 1.0;

        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:646
        // General input dampening: input.scale(0.98F)
        x *= 0.98;
        z *= 0.98;

        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:651-654
        // When sneaking (isMovingSlowly), scale input by SNEAKING_SPEED attribute (default 0.3)
        if (entity.IsSneaking)
        {
            x *= PhysicsConstants.SneakingSpeedMultiplier;
            z *= PhysicsConstants.SneakingSpeedMultiplier;
        }

        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:660-667
        // modifyInputSpeedForSquareMovement: clamp input magnitude to [0, 1]
        double length = Math.Sqrt(x * x + z * z);
        if (length > 0.0)
        {
            double clamped = length < 1.0 ? length : 1.0;
            double scale = clamped / length;
            x *= scale;
            z *= scale;
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
    private Vector3<double> HandleOnClimbable(Entity entity, Level level, Vector3<double> delta)
    {
        if (IsOnClimbable(entity, level))
        {
            // Note: Java resets fallDistance here (entity.resetFallDistance())
            // but our Entity doesn't track FallDistance yet - not critical for physics
            
            // Clamp horizontal velocity to max climb speed
            // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2535-2538
            const double maxClimbSpeed = 0.15000000596046448;
            double xd = Math.Clamp(delta.X, -maxClimbSpeed, maxClimbSpeed);
            double zd = Math.Clamp(delta.Z, -maxClimbSpeed, maxClimbSpeed);
            double yd = Math.Max(delta.Y, -maxClimbSpeed);

            // Suppress sliding down ladder when sneaking (Player only)
            // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2539-2541
            // Java: if (yd < 0.0 && !getInBlockState().is(Blocks.SCAFFOLDING) && isSuppressingSlidingDownLadder() && this instanceof Player)
            //   isSuppressingSlidingDownLadder() = isShiftKeyDown() = sneaking
            if (yd < 0.0 && entity.IsSneaking)
            {
                // Check block at feet isn't scaffolding (scaffolding allows sliding even when sneaking)
                int bx = (int)Math.Floor(entity.Position.X);
                int by = (int)Math.Floor(entity.Position.Y);
                int bz = (int)Math.Floor(entity.Position.Z);
                var blockAtFeet = level.GetBlockAt(bx, by, bz);
                if (blockAtFeet == null || !blockAtFeet.Name.Equals("minecraft:scaffolding", StringComparison.OrdinalIgnoreCase))
                {
                    yd = 0.0;
                }
            }

            return new Vector3<double>(xd, yd, zd);
        }

        return delta;
    }

    /// <summary>
    /// Set of block names that are in the BlockTags.CLIMBABLE tag.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/data/tags/VanillaBlockTagsProvider.java:64
    /// Tag: BlockTags.CLIMBABLE = ladder, vine, scaffolding, weeping_vines, weeping_vines_plant,
    ///   twisting_vines, twisting_vines_plant, cave_vines, cave_vines_plant
    /// </summary>
    private static readonly HashSet<string> ClimbableBlocks = new(StringComparer.OrdinalIgnoreCase)
    {
        "minecraft:ladder",
        "minecraft:vine",
        "minecraft:scaffolding",
        "minecraft:weeping_vines",
        "minecraft:weeping_vines_plant",
        "minecraft:twisting_vines",
        "minecraft:twisting_vines_plant",
        "minecraft:cave_vines",
        "minecraft:cave_vines_plant"
    };

    /// <summary>
    /// Checks if entity is on a climbable block (ladder, vine, etc.).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:1636-1651
    /// Java: onClimbable() checks getInBlockState().is(BlockTags.CLIMBABLE) at blockPosition()
    /// blockPosition() = (floor(x), floor(y), floor(z))
    /// </summary>
    private bool IsOnClimbable(Entity entity, Level? level)
    {
        if (level == null) return false;
        
        // Get block at entity's feet position (floor of entity position)
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:3721-3722
        int bx = (int)Math.Floor(entity.Position.X);
        int by = (int)Math.Floor(entity.Position.Y);
        int bz = (int)Math.Floor(entity.Position.Z);
        
        var blockState = level.GetBlockAt(bx, by, bz);
        if (blockState == null) return false;
        
        return ClimbableBlocks.Contains(blockState.Name);
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

        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:763
        // Back off from edge when sneaking to prevent falling off blocks
        var deltaBeforeEdge = delta;
        delta = MaybeBackOffFromEdge(entity, level, delta);

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

        // DEVIATION FROM VANILLA: Force HorizontalCollision on climbable blocks.
        // In vanilla, ladders have hasCollision=false (no collision shape), so the player walks through
        // the ladder and hits the solid wall behind it, which triggers HorizontalCollision naturally.
        // We intentionally give ladders thin collision shapes in BlockShapeRegistry so the bot collides
        // with the ladder itself (without needing to reach the wall). This means HorizontalCollision
        // may not trigger via normal collision, so we force it when movement is reduced on a climbable.
        // This complements the ladder collision shapes in BlockShapeRegistry.
        if (!entity.HorizontalCollision && IsOnClimbable(entity, level))
        {
            // If we are moving towards a block and our velocity is very low or blocked, count it as collision
            if (delta.LengthSquared() > 1e-9 && movement.LengthSquared() < delta.LengthSquared() * 0.95)
            {
                entity.HorizontalCollision = true;
            }
        }

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
    /// Prevents a sneaking player from walking off block edges.
    /// Iteratively reduces horizontal movement until the player won't fall.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/player/Player.java:857-900
    /// </summary>
    private Vector3<double> MaybeBackOffFromEdge(Entity entity, Level level, Vector3<double> delta)
    {
        double maxDownStep = PhysicsConstants.DefaultStepHeight;

        // Only apply when: not flying, not moving up, sneaking, and on or near ground
        // Reference: Player.java:858-859 conditions
        if (delta.Y > 0.0 || !entity.IsSneaking || !IsAboveGround(entity, level, maxDownStep))
        {
            return delta;
        }

        double deltaX = delta.X;
        double deltaZ = delta.Z;
        const double step = 0.05;
        double stepX = Math.Sign(deltaX) * step;
        double stepZ = Math.Sign(deltaZ) * step;

        // Reference: Player.java:866-871 - Reduce deltaX until safe
        while (deltaX != 0.0 && CanFallAtLeast(entity, level, deltaX, 0.0, maxDownStep))
        {
            if (Math.Abs(deltaX) <= step)
            {
                deltaX = 0.0;
                break;
            }
            deltaX -= stepX;
        }

        // Reference: Player.java:873-880 - Reduce deltaZ until safe
        while (deltaZ != 0.0 && CanFallAtLeast(entity, level, 0.0, deltaZ, maxDownStep))
        {
            if (Math.Abs(deltaZ) <= step)
            {
                deltaZ = 0.0;
                break;
            }
            deltaZ -= stepZ;
        }

        // Reference: Player.java:882-894 - Reduce both together until safe
        while (deltaX != 0.0 && deltaZ != 0.0 && CanFallAtLeast(entity, level, deltaX, deltaZ, maxDownStep))
        {
            if (Math.Abs(deltaX) <= step)
            {
                deltaX = 0.0;
            }
            else
            {
                deltaX -= stepX;
            }

            if (Math.Abs(deltaZ) <= step)
            {
                deltaZ = 0.0;
            }
            else
            {
                deltaZ -= stepZ;
            }
        }

        return new Vector3<double>(deltaX, delta.Y, deltaZ);
    }

    /// <summary>
    /// Checks if the entity is on the ground or close enough to be considered grounded.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/player/Player.java:902-904
    /// </summary>
    private bool IsAboveGround(Entity entity, Level level, double maxDownStep)
    {
        // We don't track fallDistance, so just check onGround or use a simplified check
        // Reference: onGround || (fallDistance < maxDownStep && !canFallAtLeast(0, 0, maxDownStep - fallDistance))
        return entity.IsOnGround || !CanFallAtLeast(entity, level, 0.0, 0.0, maxDownStep);
    }

    /// <summary>
    /// Checks if the player would fall at least minHeight if offset by (deltaX, deltaZ).
    /// Creates a thin box below the player's bounding box at the offset position and checks for collisions.
    /// Returns true if there are NO collisions (meaning the player WOULD fall).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/player/Player.java:906-909
    /// </summary>
    private bool CanFallAtLeast(Entity entity, Level level, double deltaX, double deltaZ, double minHeight)
    {
        var bb = entity.GetBoundingBox();
        // Create a box below the player at the offset position
        // The small epsilon (1.0E-7) shrinks the box slightly to avoid false positives from touching edges
        var checkBox = new AABB(
            bb.MinX + 1.0E-7 + deltaX,
            bb.MinY - minHeight - 1.0E-7,
            bb.MinZ + 1.0E-7 + deltaZ,
            bb.MaxX - 1.0E-7 + deltaX,
            bb.MinY,
            bb.MaxZ - 1.0E-7 + deltaZ);
        // noCollision = no blocks in the box = player would fall
        // Note: Ladders have no collision shape (like vanilla), so they don't provide ground support.
        // The solid ground blocks below ladders provide the support instead.
        return !level.GetCollidingBlockAABBs(checkBox).Any();
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
                // CRITICAL FIX: Step-up must move Y (up) first, then horizontally
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java
                var stepMovement = new Vector3<double>(movement.X, candidateHeight, movement.Z);
                var stepFromGround = CollideWithShapes(stepMovement, groundedAABB, stepUpShapes, new[] { Axis.Y, Axis.X, Axis.Z });
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
    private Vector3<double> CollideWithShapes(Vector3<double> movement, AABB boundingBox, List<VoxelShape> shapes, Axis[]? axisOrder = null)
    {
        if (shapes.Count == 0)
        {
            return movement;
        }

        var resolvedMovement = Vector3<double>.Zero;

        // Resolve collisions in specified order (vanilla uses axisStepOrder which optimizes, but the order matters for step-up)
        var axes = axisOrder ?? new[] { Axis.X, Axis.Y, Axis.Z };
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
        // ===== STEP 1: Send PlayerCommand + PlayerInput packets FIRST (matching Java order) =====
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java
        // Java order in tick(): sendShiftKeyState() → sendPlayerInput() → sendPosition()
        // And within sendPosition(): sendIsSprintingIfNeeded() → position packets
        // Net effect: commands + input before position packets.
        
        var currentInput = entity.InputState.Current;
        
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:288-296
        // sendIsSprintingIfNeeded() - uses dedicated wasSprinting field, NOT lastSentInput
        bool currentlySprinting = currentInput.Sprint;
        if (currentlySprinting != entity.WasSprinting)
        {
            _logger.LogInformation("[Sprint] State change: {Old} -> {New}, sending {Action}",
                entity.WasSprinting ? "SPRINTING" : "NOT_SPRINTING",
                currentlySprinting ? "SPRINTING" : "NOT_SPRINTING",
                currentlySprinting ? "StartSprint" : "StopSprint");
            await packetSender.SendPacketAsync(new PlayerCommandPacket
            {
                EntityId = entity.EntityId,
                Action = currentlySprinting ? PlayerAction.StartSprint : PlayerAction.StopSprint
            });
            entity.WasSprinting = currentlySprinting;
        }
        
        // NOTE: In 1.21.x, sneaking is communicated ONLY via PlayerInputPacket (0x2A) Shift flag.
        // The PRESS_SHIFT_KEY/RELEASE_SHIFT_KEY actions were REMOVED from ServerboundPlayerCommandPacket.
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ServerboundPlayerCommandPacket.java:57-64
        // The Shift flag in PlayerInputPacket (sent below) handles sneak state for the server.
        
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:215-217
        // Send PlayerInput packet when input flags change
        var lastSentInput = entity.InputState.LastSent;
        var keyPresses = currentInput.ToByte();
        var lastKeyPresses = lastSentInput.ToByte();
        
        if (keyPresses != lastKeyPresses)
        {
            _logger.LogDebug("[Input] Flags changed: 0x{Old:X2} -> 0x{New:X2} (Sprint={Sprint}, Forward={Forward}, Shift={Shift})",
                lastKeyPresses, keyPresses, currentInput.Sprint, currentInput.Forward, currentInput.Shift);
            var flag = (PlayerInputPacket.MovementFlag)keyPresses;
            await packetSender.SendPacketAsync(new PlayerInputPacket(flag));
            entity.InputState.LastSent = currentInput;
        }
        
        // ===== STEP 2: Send position/rotation packets =====
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:248-286
        
        double deltaX = entity.Position.X - entity.LastSentPosition.X;
        double deltaY = entity.Position.Y - entity.LastSentPosition.Y;
        double deltaZ = entity.Position.Z - entity.LastSentPosition.Z;
        double deltaYRot = entity.YawPitch.X - entity.LastSentYawPitch.X;
        double deltaXRot = entity.YawPitch.Y - entity.LastSentYawPitch.Y;

        entity.PositionReminder++;
        bool move = Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ) > PositionUpdateThreshold ||
                    entity.PositionReminder >= PositionReminderInterval;
        
        // Match vanilla exactly: any non-zero rotation change triggers a rotation packet
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:258
        bool rot = deltaYRot != 0.0 || deltaXRot != 0.0;

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
            // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:265-266
            await packetSender.SendPacketAsync(new MovePlayerStatusOnlyPacket
            {
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
