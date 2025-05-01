using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Core;

public partial class MinecraftClient
{
    #region Main Physics Logic

    private const double JumpVerticalVelocity = 0.506;
    private const double SprintJumpForwardBoost = 0.17;

    private const double Gravity = -0.08;
    private const double AirDrag = 0.98;
    private const double GroundFriction = 0.6;
    private const double Slipperiness = 0.91;

    private const double TerminalVelocity = -3.92;
    private const double BaseGroundAcceleration = 0.117;
    private const double SprintMultiplier = 1.3;
    private const double SneakMultiplier = 0.3;
    private const double AirAcceleration = 0.0037;

    private const double PlayerBoundingBoxWidth = 0.6;
    private const double PlayerCollisionRange = PlayerBoundingBoxWidth * 1.3;
    private const double PlayerCollisionPushStrength = 0.1;
    private const double MaxPushVelocity = 0.15;

    private const double KnockBackBaseStrength = 0.25;
    private const double KnockBackVerticalBoost = 0.4;

    public async Task PhysicsTickAsync()
    {
        if (!State.LocalPlayer.HasEntity) return;

        var entity = State.LocalPlayer.Entity;
        var level = State.Level;

        //Console.WriteLine($"[{TimeProvider.System.GetLocalNow():HH:mm:ss.fff}] TICK {level.ClientTickCounter}");

        UpdatePathFollowingInput(entity);
        ApplyJumpingInput(entity);
        ApplyMovementInput(entity);
        HandleEntityCollisions(entity, level);
        ApplyKnockBack(entity);

        var desiredDelta = entity.Velocity;
        var actualDelta = MoveEntityWithCollisions(entity, level, desiredDelta);
        var significantPositionChange = Math.Abs(actualDelta.X) > Epsilon ||
                                        Math.Abs(actualDelta.Y) > Epsilon ||
                                        Math.Abs(actualDelta.Z) > Epsilon;

        bool stoppedByCollisionX;
        bool stoppedByCollisionZ;
        if (!significantPositionChange && !entity.IsHurt)
        {
            stoppedByCollisionX = Math.Abs(desiredDelta.X) > Epsilon && Math.Abs(actualDelta.X) < Epsilon;
            stoppedByCollisionZ = Math.Abs(desiredDelta.Z) > Epsilon && Math.Abs(actualDelta.Z) < Epsilon;
            await UpdateSprintStateAndSendPackets(entity, (stoppedByCollisionX, stoppedByCollisionZ), false);
            return;
        }

        stoppedByCollisionX = Math.Abs(desiredDelta.X) > Epsilon && Math.Abs(actualDelta.X) < Epsilon;
        stoppedByCollisionZ = Math.Abs(desiredDelta.Z) > Epsilon && Math.Abs(actualDelta.Z) < Epsilon;

        await UpdateSprintStateAndSendPackets(entity, (stoppedByCollisionX, stoppedByCollisionZ), true);
    }

    private void ApplyJumpingInput(Entity entity)
    {
        if (entity is not { IsJumping: true, IsOnGround: true }) return;

        // Reset horizontal velocity if jumping from a path - This fixes overshooting blocks
        if (_currentPath != null)
        {
            entity.Velocity.X = 0;
            entity.Velocity.Z = 0;
        }

        entity.Velocity.Y = JumpVerticalVelocity;

        if (!entity.IsSprintingNew || !(Math.Abs(SprintJumpForwardBoost) > Epsilon)) return;

        var yawRadians = entity.YawPitch.X * (Math.PI / 180.0);
        var lookX = -Math.Sin(yawRadians);
        var lookZ = Math.Cos(yawRadians);
        var boostX = lookX * SprintJumpForwardBoost;
        var boostZ = lookZ * SprintJumpForwardBoost;
        entity.Velocity.X += boostX;
        entity.Velocity.Z += boostZ;
    }

    private void ApplyMovementInput(Entity entity)
    {
        // 1. Apply Friction/Drag
        var friction = entity.IsOnGround ? GroundFriction * Slipperiness : AirDrag;
        entity.Velocity.X *= friction;
        entity.Velocity.Z *= friction;

        // 2. Apply Gravity
        entity.Velocity.Y += Gravity;
        entity.Velocity.Y *= AirDrag;
        if (entity.Velocity.Y < TerminalVelocity)
        {
            entity.Velocity.Y = TerminalVelocity;
        }

        // 3. Calculate and Apply Input Acceleration
        double moveX = 0;
        double moveZ = 0;
        if (entity.Forward) moveZ += 1.0;
        if (entity.Backward) moveZ -= 1.0;
        if (entity.Left) moveX -= 1.0;
        if (entity.Right) moveX += 1.0;

        var inputLength = Math.Sqrt(moveX * moveX + moveZ * moveZ);

        if (inputLength > Epsilon)
        {
            moveX /= inputLength;
            moveZ /= inputLength;

            double currentTickAcceleration;
            if (entity.IsOnGround)
            {
                currentTickAcceleration = BaseGroundAcceleration;
                if (entity is { IsSprintingNew: true, IsSneaking: false })
                {
                    currentTickAcceleration *= SprintMultiplier;
                }
                else if (entity.IsSneaking)
                {
                    currentTickAcceleration *= SneakMultiplier;
                }
            }
            else
            {
                currentTickAcceleration = AirAcceleration;
            }

            var moveInfluence = currentTickAcceleration;

            var yawRadians = entity.YawPitch.X * (Math.PI / 180.0);
            var sinYaw = Math.Sin(yawRadians);
            var cosYaw = Math.Cos(yawRadians);
            var worldMoveX = (moveX * cosYaw - moveZ * sinYaw) * moveInfluence;
            var worldMoveZ = (moveX * sinYaw + moveZ * cosYaw) * moveInfluence;

            entity.Velocity.X += worldMoveX;
            entity.Velocity.Z += worldMoveZ;
        }
    }

    private void HandleEntityCollisions(Entity entity, Level level)
    {
        var allEntityIds = level.GetAllEntityIds();
        if (allEntityIds.Length is 0) return;

        foreach (var otherId in allEntityIds)
        {
            if (otherId == entity.EntityId) continue;
            var otherEntity = level.GetEntityOfId(otherId);
            if (otherEntity == null) continue;

            var dy = entity.Position.Y - otherEntity.Position.Y;
            if (Math.Abs(dy) > 1.0) continue;

            var dx = entity.Position.X - otherEntity.Position.X;
            var dz = entity.Position.Z - otherEntity.Position.Z;
            var distanceSquared = dx * dx + dz * dz;

            if (distanceSquared >= PlayerCollisionRange * PlayerCollisionRange) continue;

            var pushDistance = Math.Sqrt(distanceSquared);
            if (pushDistance < 0.01)
            {
                var randomAngle = new Random().NextDouble() * 2 * Math.PI;
                dx = Math.Cos(randomAngle);
                dz = Math.Sin(randomAngle);
                pushDistance = 0.01;
            }

            var pushDirectionX = dx / pushDistance;
            var pushDirectionZ = dz / pushDistance;

            var pushStrength = PlayerCollisionPushStrength * (1.0 - (pushDistance / PlayerCollisionRange));

            var pushX = pushDirectionX * pushStrength;
            var pushZ = pushDirectionZ * pushStrength;

            pushX = Math.Clamp(pushX, -MaxPushVelocity, MaxPushVelocity);
            pushZ = Math.Clamp(pushZ, -MaxPushVelocity, MaxPushVelocity);

            entity.Velocity.X += pushX;
            entity.Velocity.Z += pushZ;
        }
    }

    private void ApplyKnockBack(Entity entity)
    {
        if (!entity.IsHurt) return;
        var lookingYaw = entity.YawPitch.X + 90;
        var hurtFromYaw = entity.IsHurtFromYaw.Value;

        var lookingRadians = lookingYaw * (Math.PI / 180);
        var hurtRadians = hurtFromYaw * (Math.PI / 180);
        var attackAngle = lookingRadians + hurtRadians;

        entity.Velocity.X += -Math.Sin(attackAngle) * KnockBackBaseStrength;
        entity.Velocity.Z += Math.Cos(attackAngle) * KnockBackBaseStrength;
        entity.Velocity.Y += KnockBackVerticalBoost;

        entity.IsHurtFromYaw = null;
    }

    private async Task UpdateSprintStateAndSendPackets(Entity entity, (bool collidedX, bool collidedZ) collisionFlags,
        bool sendPositionPacket)
    {
        var wantsToMove = entity.Forward || entity.Backward || entity.Left || entity.Right;

        if (entity is { WantsToSprint: true, IsSprintingNew: false } && wantsToMove && entity.Forward &&
            collisionFlags is { collidedX: false, collidedZ: false } && entity.Hunger > 6)
        {
            await SendPacketAsync(new PlayerCommandPacket { EntityId = entity.EntityId, Action = PlayerAction.StartSprint });
            entity.IsSprintingNew = true;
        }
        else if (entity.IsSprintingNew && (!wantsToMove || !entity.Forward || collisionFlags.collidedX || collisionFlags.collidedZ ||
                                           !entity.WantsToSprint || entity.Hunger <= 6))
        {
            await SendPacketAsync(new PlayerCommandPacket { EntityId = entity.EntityId, Action = PlayerAction.StopSprint });
            entity.IsSprintingNew = false;
        }

        var flags = MovementFlags.None;
        if (entity.IsOnGround)
        {
            flags = MovementFlags.OnGround;
        }

        if (sendPositionPacket)
        {
            await SendPacketAsync(new MovePlayerPositionRotationPacket
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

    #endregion

    #region Collision

    private const double Epsilon = 1.0E-7;
    private const double StepHeight = 0.6;

    /// <summary>
    /// Moves the entity by the given delta, checking for collisions with world blocks.
    /// Modifies the entity's Position, Velocity (zeroing components on collision), and IsOnGround state.
    /// Follows Minecraft's axis-separation collision resolution.
    /// Includes step-up logic.
    /// </summary>
    /// <param name="entity">The entity to move.</param>
    /// <param name="level">The world level.</param>
    /// <param name="delta">The desired movement vector for this tick.</param>
    /// <returns>The actual delta movement applied after collisions.</returns>
    private Vector3<double> MoveEntityWithCollisions(Entity entity, Level level, Vector3<double> delta)
    {
        var originalBoundingBox = entity.GetBoundingBox();
        var currentBoundingBox = originalBoundingBox;
        var wasOnGround = entity.IsOnGround;
        var landedThisTick = false;

        // 1. Get Potential Colliders
        var expandedBox = currentBoundingBox.Expand(
            delta.X > 0 ? delta.X : 0,
            delta.Y > 0 ? delta.Y : 0,
            delta.Z > 0 ? delta.Z : 0
        ).Expand(
            delta.X < 0 ? -delta.X : 0,
            delta.Y < 0 ? -delta.Y : 0,
            delta.Z < 0 ? -delta.Z : 0
        ).Expand(Epsilon);

        var potentialColliders = level.GetCollidingBlockAABBs(expandedBox);
        var originalDeltaX = delta.X;
        var originalDeltaZ = delta.Z;

        // 2. Resolve Y-Axis Collision
        var adjustedDeltaY = potentialColliders
            .Aggregate(delta.Y, (current, blockBox) => currentBoundingBox.CalculateYOffset(blockBox, current));

        if (Math.Abs(adjustedDeltaY - delta.Y) > Epsilon)
        {
            if (delta.Y < 0)
            {
                landedThisTick = true;
            }

            entity.Velocity.Y = 0;
        }

        currentBoundingBox = currentBoundingBox.Offset(0, adjustedDeltaY, 0);
        delta.Y = adjustedDeltaY;
        entity.IsOnGround = landedThisTick;

        // 3. Resolve X-Axis Collision
        var adjustedDeltaX = potentialColliders
            .Aggregate(delta.X, (current, blockBox) => currentBoundingBox.CalculateXOffset(blockBox, current));
        currentBoundingBox = currentBoundingBox.Offset(adjustedDeltaX, 0, 0);
        if (Math.Abs(adjustedDeltaX - delta.X) > Epsilon)
        {
            entity.Velocity.X = 0;
        }

        delta.X = adjustedDeltaX;

        // 4. Resolve Z-Axis Collision
        var adjustedDeltaZ = potentialColliders
            .Aggregate(delta.Z, (current, blockBox) => currentBoundingBox.CalculateZOffset(blockBox, current));
        currentBoundingBox = currentBoundingBox.Offset(0, 0, adjustedDeltaZ);
        if (Math.Abs(adjustedDeltaZ - delta.Z) > Epsilon)
        {
            entity.Velocity.Z = 0;
        }

        delta.Z = adjustedDeltaZ;

        var collidedHorizontally = (Math.Abs(delta.X - originalDeltaX) > Epsilon || Math.Abs(delta.Z - originalDeltaZ) > Epsilon);
        if (collidedHorizontally && wasOnGround && !entity.IsSneaking)
        {
            var stepAttemptDelta = PerformStepUp(level, originalBoundingBox, originalDeltaX, originalDeltaZ, potentialColliders);
            var stepSqDist = stepAttemptDelta.X * stepAttemptDelta.X + stepAttemptDelta.Z * stepAttemptDelta.Z;
            var initialSqDist = delta.X * delta.X + delta.Z * delta.Z;

            if (stepSqDist > initialSqDist + Epsilon)
            {
                delta = new Vector3<double>(stepAttemptDelta.X, stepAttemptDelta.Y, stepAttemptDelta.Z);
                currentBoundingBox = originalBoundingBox.Offset(delta);
                entity.IsOnGround = true;
                entity.Velocity.Y = 0;
            }
        }

        // 5. Final Position Update
        entity.UpdatePositionFromAABB(currentBoundingBox);
        return new Vector3<double>(delta.X, delta.Y, delta.Z);
    }

    /// <summary>
    /// Attempts to perform a step-up maneuver when horizontal movement is blocked while grounded.
    /// </summary>
    /// <param name="level">The world level.</param>
    /// <param name="originalBox">The entity's bounding box before any movement this tick.</param>
    /// <param name="desiredDeltaX">The originally intended horizontal movement in X.</param>
    /// <param name="desiredDeltaZ">The originally intended horizontal movement in Z.</param>
    /// <param name="potentialColliders">List of blocks near the movement path (optimization).</param>
    /// <returns>The calculated delta vector for the step-up, or Zero if stepping failed.</returns>
    private Vector3<double> PerformStepUp(Level level, AABB originalBox, double desiredDeltaX, double desiredDeltaZ,
        List<AABB> potentialColliders)
    {
        // --- 1. Check Vertical Clearance ---
        var stepCheckBox = originalBox.Offset(0, StepHeight + Epsilon, 0);
        bool canStepUp = potentialColliders.All(blockBox => !blockBox.Intersects(stepCheckBox));

        if (canStepUp)
        {
            var checkRegion = originalBox.Expand(0, StepHeight, 0).Offset(0, Epsilon, 0);
            var ceilingColliders = level.GetCollidingBlockAABBs(checkRegion);
            if (ceilingColliders.Any(blockBox => blockBox.Max.Y > originalBox.Max.Y + Epsilon && blockBox.Intersects(stepCheckBox)))
            {
                canStepUp = false;
            }
        }

        if (!canStepUp)
        {
            return Vector3<double>.Zero;
        }

        // --- 2. Perform Step Movement Sequence ---
        var horizontalStepX = desiredDeltaX;
        var horizontalStepZ = desiredDeltaZ;

        var currentStepY = StepHeight;
        var stepUpBox = originalBox;
        var stepUpColliders = level.GetCollidingBlockAABBs(stepUpBox.Offset(0, currentStepY, 0).Expand(Epsilon));
        currentStepY = stepUpColliders.Aggregate(currentStepY, (current, blockBox) => stepUpBox.CalculateYOffset(blockBox, current));
        stepUpBox = stepUpBox.Offset(0, currentStepY, 0);

        var currentStepX = horizontalStepX;
        var stepXColliders = level.GetCollidingBlockAABBs(stepUpBox.Offset(currentStepX, 0, 0).Expand(Epsilon));
        currentStepX = stepXColliders.Aggregate(currentStepX, (current, blockBox) => stepUpBox.CalculateXOffset(blockBox, current));
        stepUpBox = stepUpBox.Offset(currentStepX, 0, 0);

        var currentStepZ = horizontalStepZ;
        var stepZColliders = level.GetCollidingBlockAABBs(stepUpBox.Offset(0, 0, currentStepZ).Expand(Epsilon));
        currentStepZ = stepZColliders.Aggregate(currentStepZ, (current, blockBox) => stepUpBox.CalculateZOffset(blockBox, current));
        stepUpBox = stepUpBox.Offset(0, 0, currentStepZ);


        // --- 3. Settle Down onto the Step ---
        double finalDownY = 0;
        var checkDownDist = currentStepY + 1.0;
        var checkDownBox = stepUpBox.Offset(0, -checkDownDist, 0);
        var downColliders = level.GetCollidingBlockAABBs(checkDownBox);

        var highestGroundY = originalBox.Min.Y - 1.0;
        var foundGround = false;
        foreach (var blockBox in downColliders)
        {
            if (!(blockBox.Max.Y <= stepUpBox.Min.Y + Epsilon) || !(blockBox.Max.Y > highestGroundY)) continue;
            if (!(blockBox.Max.X > stepUpBox.Min.X) || !(blockBox.Min.X < stepUpBox.Max.X) ||
                !(blockBox.Max.Z > stepUpBox.Min.Z) || !(blockBox.Min.Z < stepUpBox.Max.Z)) continue;
            highestGroundY = blockBox.Max.Y;
            foundGround = true;
        }

        if (foundGround)
        {
            finalDownY = highestGroundY - stepUpBox.Min.Y;
            if (finalDownY > Epsilon) finalDownY = 0;
            if (finalDownY < -checkDownDist) finalDownY = -checkDownDist;
        }
        else
        {
            finalDownY = 0;
        }

        stepUpBox = stepUpBox.Offset(0, finalDownY, 0);

        // --- 4. Calculate Final Delta ---
        var finalDelta = stepUpBox.Min - originalBox.Min;
        return finalDelta;
    }

    #endregion
}
