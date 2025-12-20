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
        prePhysicsCallback?.Invoke(entity);

        // 1.5 Protocol Sync: Handle pending teleport
        if (entity.HasPendingTeleport)
        {
            entity.HasPendingTeleport = false;
            
            // Perform a quick ground check for the current (teleported) position
            // This ensures we send the correct OnGround state in the sync packet.
            var collision = CollisionResolver.MoveWithCollisions(entity.GetBoundingBox(), level, new Vector3<double>(0, -0.05, 0), false, false);
            
            // Heuristic for initial spawn: If chunks aren't loaded, don't claim to be falling if Y is an integer.
            // This prevents the server from rejecting our position and creating a teleport loop.
            int chunkX = (int)Math.Floor(entity.Position.X) >> 4;
            int chunkZ = (int)Math.Floor(entity.Position.Z) >> 4;
            if (!level.HasChunk(chunkX, chunkZ))
            {
                // If chunk is missing, we can't detect ground. 
                // Assume grounded if Y is an integer (classic floor level like 64.0)
                entity.IsOnGround = Math.Abs(entity.Position.Y - Math.Floor(entity.Position.Y)) < 0.001;
                Console.WriteLine($"[TELEPORT_DEBUG] Chunk ({chunkX}, {chunkZ}) missing for teleport sync. Heuristic Gnd={entity.IsOnGround} (PosY={entity.Position.Y:F4})");
            }
            else
            {
                entity.IsOnGround = collision.LandedOnGround;
                Console.WriteLine($"[TELEPORT_DEBUG] Chunk loaded. Detected Gnd={entity.IsOnGround}");
            }

            var flags = entity.IsOnGround ? MovementFlags.OnGround : MovementFlags.None;
            if (entity.HorizontalCollision) flags |= MovementFlags.HorizontalCollision;

            await sendPacketAsync(new MovePlayerPositionRotationPacket
            {
                X = entity.Position.X,
                Y = entity.Position.Y,
                Z = entity.Position.Z,
                Yaw = entity.YawPitch.X,
                Pitch = entity.YawPitch.Y,
                Flags = flags
            });
            
            Console.WriteLine($"[TELEPORT_DEBUG] Sent teleport acknowledgement sync packet: Pos={entity.Position}, OnGround={entity.IsOnGround}");
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
                Flags = entity.IsOnGround ? MovementFlags.OnGround : MovementFlags.None
            });
            return;
        }

        // 2. Capture block friction and fluid state at start of tick
        var blockFriction = MovementCalculator.GetBlockFriction(level, entity.Position);
        UpdateFluidState(entity, level);

        if (entity.IsInWater || entity.Velocity.Y != 0)
        {
            Console.WriteLine($"[PHYSICS_DEBUG] Tick start: Pos={entity.Position}, Vel={entity.Velocity}, InWater={entity.IsInWater}, FluidH={entity.FluidHeight:F2}, Jmp={entity.IsJumping}, Gnd={entity.IsOnGround}");
        }

        // 3. Handle Jump / Swimming (Source: LivingEntity.aiStep)
        if (entity.IsJumping)
        {
            if (entity.IsInWater || entity.IsInLava)
            {
                // Swim up if in liquid. Matching Java's unconditional jumpInLiquid() behavior.
                // We trust IsInWater/IsInLava which relies on bounding box intersection.
                if (true) // Simplification: If we are in the block, we can swim.
                {
                    entity.Velocity = MovementCalculator.ApplyFluidJump(entity.Velocity);
                    Console.WriteLine($"[PHYSICS_DEBUG] Swimming Up: NewVelY={entity.Velocity.Y:F4}");
                }
                else if (entity.IsOnGround)
                {
                    // Regular jump if in shallow liquid on ground
                    entity.Velocity = MovementCalculator.ApplyJump(
                        entity.Velocity,
                        entity.YawPitch.X,
                        entity.IsSprinting,
                        BaseJumpPower);
                    Console.WriteLine($"[PHYSICS_DEBUG] Shallow Fluid Jump: NewVelY={entity.Velocity.Y:F4}");
                }
            }
            else if (entity.IsOnGround)
            {
                // Standard ground jump
                entity.Velocity = MovementCalculator.ApplyJump(
                    entity.Velocity,
                    entity.YawPitch.X,
                    entity.IsSprinting,
                    BaseJumpPower);
            }
        }

        // 4. Calculate Input Acceleration
        var (moveX, moveZ) = entity.Input.GetNormalizedMoveVector();
        if (Math.Abs(moveX) > 0.0001f || Math.Abs(moveZ) > 0.0001f)
        {
            var effectiveSpeed = MovementCalculator.GetEffectiveSpeed(
                BaseMovementSpeed,
                entity.IsSprinting,
                entity.IsSneaking);

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

        // 5. Handle Climbing (Source: LivingEntity.handleOnClimbable)
        entity.Velocity = MovementCalculator.HandleClimbing(entity.Velocity, false, entity.IsSneaking);

        // 6. Apply Entity Collisions
        var pushVelocity = CollisionResolver.ResolveEntityCollisions(entity, level);
        entity.Velocity += pushVelocity;

        // 7. Apply Knockback
        ApplyKnockback(entity);

        // 8. MOVE with collision resolution
        var collisionResult = MoveWithCollisions(entity, level);

        // 9. Update state from collision result
        entity.IsOnGround = collisionResult.LandedOnGround;
        entity.HorizontalCollision = collisionResult.HorizontalCollision;

        // 10. POST-MOVE: Apply Gravity and Friction / Damping
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

        // 11. Sprint state and packets
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

        // Start sprinting conditions
        if (entity.WantsToSprint && !entity.IsSprinting &&
            wantsToMove && hasForwardImpulse &&
            !collision.HorizontalCollision && canSprint)
        {
            entity.IsSprinting = true;
        }
        // Stop sprinting conditions
        else if (entity.IsSprinting &&
                 (!wantsToMove || !hasForwardImpulse ||
                  collision.HorizontalCollision || !entity.WantsToSprint ||
                  entity.Hunger <= 6))
        {
            entity.IsSprinting = false;
        }
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

        var flags = entity.IsOnGround ? MovementFlags.OnGround : MovementFlags.None;
        if (entity.HorizontalCollision)
        {
            flags |= MovementFlags.HorizontalCollision;
        }

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
