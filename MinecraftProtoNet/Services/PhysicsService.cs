using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Physics;
using MinecraftProtoNet.State;
using static MinecraftProtoNet.Physics.PhysicsConstants;

namespace MinecraftProtoNet.Services;

/// <summary>
/// Handles physics simulation for entities.
/// Orchestrates MovementCalculator and CollisionResolver.
/// Based on Java's LivingEntity movement methods.
/// </summary>
public class PhysicsService : IPhysicsService
{
    private readonly ILogger<PhysicsService> _logger = LoggingConfiguration.CreateLogger<PhysicsService>();

    /// <summary>
    /// Performs a physics tick for the given entity.
    /// </summary>
    public async Task PhysicsTickAsync(
        Entity entity,
        Level level,
        Func<IServerboundPacket, Task> sendPacketAsync,
        Action<Entity>? prePhysicsCallback = null)
    {
        // 1. Pre-physics callback (e.g., pathfinding input)
        // CRITICAL: We skip this if we have a pending teleport to avoid the pathfinder
        // changing our Yaw/Pitch before we acknowledge the server's teleport.
        if (!entity.HasPendingTeleport)
        {
            prePhysicsCallback?.Invoke(entity);
        }

        // 1.5 Protocol Sync: Handle pending teleport
        if (entity.HasPendingTeleport)
        {
            entity.HasPendingTeleport = false;
            
            // Perform a quick ground check for the current (teleported) position
            var collision = CollisionResolver.MoveWithCollisions(entity.GetBoundingBox(), level, new Vector3<double>(0, -0.05, 0), false, false);
            
            // Diagnostic: What block are we actually standing on?
            var blockBelow = level.GetBlockAt((int)Math.Floor(entity.Position.X), (int)Math.Floor(entity.Position.Y - 0.01), (int)Math.Floor(entity.Position.Z));
            if (blockBelow is { IsAir: true })
            {
                // If direct feet is air, check slightly deeper (e.g. 0.1 below) to see if we are standing on a partial block
                var deeperBlock = level.GetBlockAt((int)Math.Floor(entity.Position.X), (int)Math.Floor(entity.Position.Y - 0.1), (int)Math.Floor(entity.Position.Z));
                if (deeperBlock is { IsAir: false }) blockBelow = deeperBlock;
            }
            
            // Heuristic for initial spawn: If chunks aren't loaded, don't claim to be falling if Y is an integer.
            int chunkX = (int)Math.Floor(entity.Position.X) >> 4;
            int chunkZ = (int)Math.Floor(entity.Position.Z) >> 4;
            if (!level.HasChunk(chunkX, chunkZ))
            {
                entity.IsOnGround = Math.Abs(entity.Position.Y - Math.Floor(entity.Position.Y)) < 0.001;
                _logger.LogDebug("Chunk ({ChunkX}, {ChunkZ}) missing for teleport sync. Heuristic Ground={IsOnGround} (PosY={PositionY:F4})",
                    chunkX, chunkZ, entity.IsOnGround, entity.Position.Y);
            }
            else
            {
                // Scan the bounding box area for ground (to handle standing on edges)
                // We use a larger vertical search (0.5) to find the ground if we are floating slightly above it
                var scanGround = false;
                var box = entity.GetBoundingBox();
                
                // First check center
                 var centerCheck = CollisionResolver.MoveWithCollisions(
                            box, 
                            level, 
                            new Vector3<double>(0, -0.6, 0), // Check down up to step height
                            false, false);
                 
                 if (centerCheck.LandedOnGround)
                 {
                     scanGround = true;
                 }
                 else
                 {
                     // Check corners if center failed
                     for (var dx = -0.3; dx <= 0.3; dx += 0.3)
                     {
                        for (var dz = -0.3; dz <= 0.3; dz += 0.3)
                        {
                            if (Math.Abs(dx) < 0.01 && Math.Abs(dz) < 0.01) continue; // Skip center
                            
                            var checkResult = CollisionResolver.MoveWithCollisions(
                                box.Offset(dx, 0, dz), 
                                level, 
                                new Vector3<double>(0, -0.6, 0), 
                                false, false);
                            if (checkResult.LandedOnGround)
                            {
                                scanGround = true;
                                break;
                            }
                        }
                        if (scanGround) break;
                     }
                 }

                entity.IsOnGround = scanGround;
                _logger.LogDebug("Chunk loaded. Detected Ground={IsOnGround} at {Pos}. BlockBelow: {Block}. ScanDepth=0.6", 
                    entity.IsOnGround, entity.Position, blockBelow?.Name ?? "NULL");
            }

            // USE THE PRESERVED ROTATION if available to ensure 100% precision in sync
            var syncYaw = entity.TeleportYawPitch?.X ?? entity.YawPitch.X;
            var syncPitch = entity.TeleportYawPitch?.Y ?? entity.YawPitch.Y;
            entity.TeleportYawPitch = null; // Clear it

            await sendPacketAsync(new MovePlayerPositionRotationPacket
            {
                X = entity.Position.X,
                Y = entity.Position.Y,
                Z = entity.Position.Z,
                Yaw = syncYaw,
                Pitch = syncPitch,
                Flags = entity.IsOnGround ? MovementFlags.OnGround : MovementFlags.None
            });
            
            _logger.LogDebug("Sent teleport acknowledgement sync packet: Position={Position}, Rotation=({Yaw:F2}, {Pitch:F2}), OnGround={IsOnGround}",
                entity.Position, syncYaw, syncPitch, entity.IsOnGround);
            return;
        }

        // 1.7 World Loading Check: Skip physics if center chunk is not loaded
        int curChunkX = (int)Math.Floor(entity.Position.X) >> 4;
        int curChunkZ = (int)Math.Floor(entity.Position.Z) >> 4;
        if (!level.HasChunk(curChunkX, curChunkZ))
        {
            // If the world isn't loaded yet, stay at the current position.
            // This prevents falling through the virtual world and getting into teleport loops.
            // Use the same OnGround state as before to avoid triggering server-side move checks.
            await sendPacketAsync(new MovePlayerPositionRotationPacket
            {
                X = entity.Position.X,
                Y = entity.Position.Y,
                Z = entity.Position.Z,
                Yaw = entity.YawPitch.X,
                Pitch = entity.YawPitch.Y,
                Flags = (entity.IsOnGround ? MovementFlags.OnGround : MovementFlags.None) |
                        (entity.HorizontalCollision ? MovementFlags.HorizontalCollision : MovementFlags.None)
            });
            return;
        }

        // 2. Capture block friction, speed factor, jump factor, and fluid state at start of tick
        var blockFriction = MovementCalculator.GetBlockFriction(level, entity.Position);
        var speedFactor = MovementCalculator.GetBlockSpeedFactor(level, entity.Position);
        var jumpFactor = MovementCalculator.GetBlockJumpFactor(level, entity.Position);
        UpdateFluidState(entity, level);

        if (entity.IsInWater || entity.Velocity.Y != 0)
        {
            _logger.LogTrace("Tick start: Position={Position}, Velocity={Velocity}, InWater={InWater}, FluidHeight={FluidHeight:F2}, Jump={IsJumping}, Ground={IsOnGround}",
                entity.Position, entity.Velocity, entity.IsInWater, entity.FluidHeight, entity.IsJumping, entity.IsOnGround);
        }

        // 3. Decrement jump cooldown (Source: LivingEntity.aiStep noJumpDelay)
        if (entity.JumpCooldown > 0)
        {
            entity.JumpCooldown--;
        }

        // 4. Handle Jump / Swimming (Source: LivingEntity.aiStep)
        if (entity.IsJumping)
        {
            if (entity.IsInWater || entity.IsInLava)
            {
                // Swim up if in liquid (no cooldown for swimming)
                entity.Velocity = MovementCalculator.ApplyFluidJump(entity.Velocity);
                _logger.LogTrace("Swimming up: NewVelY={VelocityY:F4}", entity.Velocity.Y);
            }
            else if (entity.IsOnGround && entity.JumpCooldown == 0)
            {
                // Standard ground jump with cooldown (Source: LivingEntity line 2922-2924)
                var oldVel = entity.Velocity;
                entity.Velocity = MovementCalculator.ApplyJump(
                    entity.Velocity,
                    entity.YawPitch.X,
                    entity.IsSprinting,
                    BaseJumpPower,
                    jumpFactor);
                entity.JumpCooldown = 10; // Matches Mojang's noJumpDelay = 10
                _logger.LogDebug("Jumped: OldVel={OldVel}, NewVel={NewVel}, JumpFactor={JumpFactor}", oldVel, entity.Velocity, jumpFactor);
            }
        }

        // 5. Calculate Input Acceleration (with speed factor applied)
        var (moveX, moveZ) = entity.Input.GetNormalizedMoveVector();
        if (Math.Abs(moveX) > 0.0001f || Math.Abs(moveZ) > 0.0001f)
        {
            var effectiveSpeed = MovementCalculator.GetEffectiveSpeed(
                BaseMovementSpeed,
                entity.IsSprinting,
                entity.IsSneaking);

            // Apply block speed factor (soul sand, honey slow movement)
            effectiveSpeed *= speedFactor;

            var frictionSpeed = MovementCalculator.GetFrictionInfluencedSpeed(
                blockFriction,
                effectiveSpeed,
                entity.IsOnGround);

            var acceleration = MovementCalculator.CalculateInputAcceleration(
                moveX,
                moveZ,
                entity.YawPitch.X,
                frictionSpeed);

            entity.Velocity += acceleration;
        }

        // 6. Handle Climbing (Source: LivingEntity.handleOnClimbable)
        entity.Velocity = MovementCalculator.HandleClimbing(entity.Velocity, false, entity.IsSneaking);

        // 7. Apply Entity Collisions
        var pushVelocity = CollisionResolver.ResolveEntityCollisions(entity, level);
        entity.Velocity += pushVelocity;

        // 8. Apply Knockback
        ApplyKnockback(entity);

        // 9. MOVE with collision resolution
        var collisionResult = MoveWithCollisions(entity, level);

        // 10. Update state from collision result
        entity.IsOnGround = collisionResult.LandedOnGround;
        entity.HorizontalCollision = collisionResult.HorizontalCollision;

        // 11. POST-MOVE: Apply Gravity and Friction / Damping
        if (entity.IsInWater || entity.IsInLava)
        {
            entity.Velocity = MovementCalculator.ApplyFluidPhysics(
                entity.Velocity,
                DefaultGravity,
                entity.IsSprinting,
                entity.IsInWater,
                entity.IsInLava);
        }
        else
        {
            // Horizontal friction
            entity.Velocity = MovementCalculator.ApplyHorizontalFriction(
                entity.Velocity,
                blockFriction,
                entity.IsOnGround);

            // Gravity and vertical drag
            entity.Velocity = MovementCalculator.ApplyGravity(entity.Velocity, DefaultGravity);
        }

        // Final clamp to prevent jitter
        entity.Velocity = MovementCalculator.ClampMinimumMovement(entity.Velocity);

        // 12. Sprint state and packets
        UpdateSprintState(entity, collisionResult);
        await SendMovementPacketsAsync(entity, collisionResult, sendPacketAsync);
    }

    private void UpdateFluidState(Entity entity, Level level)
    {
        var box = entity.GetBoundingBox();
        var minX = (int)Math.Floor(box.Min.X);
        var minY = (int)Math.Floor(box.Min.Y);
        var minZ = (int)Math.Floor(box.Min.Z);
        var maxX = (int)Math.Floor(box.Max.X);
        var maxY = (int)Math.Floor(box.Max.Y);
        var maxZ = (int)Math.Floor(box.Max.Z);

        var inWater = false;
        var inLava = false;
        var maxFluidHeight = 0.0;

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                for (var z = minZ; z <= maxZ; z++)
                {
                    var block = level.GetBlockAt(x, y, z);
                    if (block == null) continue;

                    var isWater = block.Name.Contains("water", StringComparison.OrdinalIgnoreCase);
                    var isLava = block.Name.Contains("lava", StringComparison.OrdinalIgnoreCase);

                    if (isWater || isLava)
                    {
                        // Check if entity's AABB intersects the fluid block
                        var blockBox = new AABB(x, y, z, x + 1, y + 1, z + 1);
                        if (box.Intersects(blockBox))
                        {
                            if (isWater) inWater = true;
                            if (isLava) inLava = true;
                            
                            // Height of fluid in this column relative to the entity's feet
                            var height = (y + 1) - box.Min.Y;
                            maxFluidHeight = Math.Max(maxFluidHeight, height);
                        }
                    }
                }
            }
        }

        entity.IsInWater = inWater;
        entity.IsInLava = inLava;
        entity.FluidHeight = maxFluidHeight;
    }



    /// <summary>
    /// Applies knockback velocity if the entity was hurt.
    /// </summary>
    private void ApplyKnockback(Entity entity)
    {
        if (!entity.IsHurt || entity.HurtFromYaw is not { } hurtYaw)
        {
            return;
        }

        // Calculate knockback direction
        var attackAngle = (entity.YawPitch.X + 90 + hurtYaw) * (Math.PI / 180);

        entity.Velocity = new Vector3<double>(
            entity.Velocity.X - Math.Sin(attackAngle) * DefaultKnockback,
            entity.Velocity.Y + DefaultKnockback, // Vertical boost
            entity.Velocity.Z + Math.Cos(attackAngle) * DefaultKnockback
        );

        // Clear hurt state
        entity.HurtFromYaw = null;
    }

    /// <summary>
    /// Moves the entity with collision resolution.
    /// </summary>
    private CollisionResult MoveWithCollisions(Entity entity, Level level)
    {
        var result = CollisionResolver.MoveWithCollisions(
            entity.GetBoundingBox(),
            level,
            entity.Velocity,
            entity.IsOnGround,
            entity.IsSneaking,
            entity.IsInWater || entity.IsInLava);

        // Update entity position
        entity.UpdatePositionFromAABB(result.FinalBoundingBox);

        // Zero out velocity components that collided
        if (result.CollidedX || result.CollidedY || result.CollidedZ)
        {
            _logger.LogTrace("Collision detected: X={X}, Y={Y}, Z={Z}, landed={Landed}", 
                result.CollidedX, result.CollidedY, result.CollidedZ, result.LandedOnGround);
        }

        if (result.CollidedX)
        {
            entity.Velocity = new Vector3<double>(0, entity.Velocity.Y, entity.Velocity.Z);
        }

        if (result.CollidedY)
        {
            entity.Velocity = new Vector3<double>(entity.Velocity.X, 0, entity.Velocity.Z);
        }

        if (result.CollidedZ)
        {
            entity.Velocity = new Vector3<double>(entity.Velocity.X, entity.Velocity.Y, 0);
        }

        return result;
    }

    /// <summary>
    /// Updates sprint state based on input and collisions.
    /// Source: Java's LocalPlayer.sendIsSprintingIfNeeded()
    /// </summary>
    private void UpdateSprintState(Entity entity, CollisionResult collision)
    {
        var wantsToMove = entity.Input.HasMovement;
        var hasForwardImpulse = entity.Input.HasForwardImpulse;
        var canSprint = entity.Hunger > 6 && !entity.IsSneaking;
        
        var isMinorCollision = collision.HorizontalCollision && IsHorizontalCollisionMinor(entity, collision.ActualDelta);

        // Start sprinting conditions
        // Matches LocalPlayer.canStartSprinting: does not check horizontalCollision
        if (entity.WantsToSprint && !entity.IsSprinting &&
            wantsToMove && hasForwardImpulse && canSprint && !entity.IsInWater)
        {
            entity.IsSprinting = true;
        }
        // Stop sprinting conditions
        // Matches LocalPlayer.shouldStopRunSprinting: only stop if collision is NOT minor
        else if (entity.IsSprinting &&
                 (!wantsToMove || !hasForwardImpulse ||
                  (collision.HorizontalCollision && !isMinorCollision) || 
                  !entity.WantsToSprint ||
                  entity.Hunger <= 6 || entity.IsInWater))
        {
            entity.IsSprinting = false;
        }
    }

    /// <summary>
    /// Checks if a horizontal collision is "minor" (scraping against a wall at a shallow angle).
    /// Prevents losing sprint momentum when brushing against blocks.
    /// Based on LocalPlayer.isHorizontalCollisionMinor.
    /// </summary>
    private bool IsHorizontalCollisionMinor(Entity entity, Vector3<double> movement)
    {
        var (moveX, moveZ) = entity.Input.GetMoveVector();
        float yawRad = entity.YawPitch.X * (MathF.PI / 180f);
        float sinYaw = MathF.Sin(yawRad);
        float cosYaw = MathF.Cos(yawRad);

        // Calculate global desired movement acceleration components (globalXA, globalZA in LocalPlayer.java)
        // xxa = moveVector.X (strafe), zza = moveVector.Z (forward)
        double globalXA = moveX * cosYaw - moveZ * sinYaw;
        double globalZA = moveZ * cosYaw + moveX * sinYaw;

        double desiredLenSq = globalXA * globalXA + globalZA * globalZA;
        double actualLenSq = movement.X * movement.X + movement.Z * movement.Z;

        if (desiredLenSq < 1e-5 || actualLenSq < 1e-5)
            return false;

        double dot = globalXA * movement.X + globalZA * movement.Z;
        double magnitudeProduct = Math.Sqrt(desiredLenSq * actualLenSq);
        
        // Math.Acos throws NaN if dot/mag is slightly outside [-1, 1] due to precision
        double cosAngle = Math.Clamp(dot / magnitudeProduct, -1.0, 1.0);
        double angle = Math.Acos(cosAngle);

        // MINOR_COLLISION_ANGLE_THRESHOLD_RADIAN = 0.13962634 (approx 8 degrees)
        return angle < 0.13962634;
    }

    /// <summary>
    /// Sends movement packets to the server.
    /// Source: Java's LocalPlayer.sendPosition() and sendIsSprintingIfNeeded()
    /// </summary>
    private async Task SendMovementPacketsAsync(
        Entity entity,
        CollisionResult collision,
        Func<IServerboundPacket, Task> sendPacketAsync)
    {
        // Send sprint state change if needed
        if (entity.IsSprinting != entity.WasSprinting)
        {
            var action = entity.IsSprinting
                ? PlayerAction.StartSprint
                : PlayerAction.StopSprint;

            await sendPacketAsync(new PlayerCommandPacket
            {
                EntityId = entity.EntityId,
                Action = action
            });

            entity.WasSprinting = entity.IsSprinting;
        }

        // Send position packet if we moved
        var actualDelta = collision.ActualDelta;
        var significantMove = Math.Abs(actualDelta.X) > Epsilon ||
                              Math.Abs(actualDelta.Y) > Epsilon ||
                              Math.Abs(actualDelta.Z) > Epsilon;

        if (!significantMove && !entity.IsHurt)
        {
            return;
        }

        var flags = (entity.IsOnGround ? MovementFlags.OnGround : MovementFlags.None) |
                    (entity.HorizontalCollision ? MovementFlags.HorizontalCollision : MovementFlags.None);

        await sendPacketAsync(new MovePlayerPositionRotationPacket
        {
            X = entity.Position.X,
            Y = entity.Position.Y,
            Z = entity.Position.Z,
            Yaw = entity.YawPitch.X,
            Pitch = entity.YawPitch.Y,
            Flags = flags
        });
    }
}
