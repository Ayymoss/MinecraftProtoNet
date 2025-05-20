using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.State;
using MinecraftProtoNet.State.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MinecraftProtoNet.Services
{
    public class PhysicsService : IPhysicsService
    {
        // Constants copied from MinecraftClient.Physics.cs
        private const double JumpVerticalVelocity = 0.506;
        private const double SprintJumpForwardBoost = 0.17;

        private const double Gravity = -0.08;
        private const double AirDrag = 0.98;
        private const double GroundFriction = 0.6;
        private const double Slipperiness = 0.91; // Typically from block properties, but used as a general factor here

        private const double TerminalVelocity = -3.92;
        private const double BaseGroundAcceleration = 0.117;
        private const double SprintMultiplier = 1.3;
        private const double SneakMultiplier = 0.3;
        private const double AirAcceleration = 0.0037;

        private const double PlayerBoundingBoxWidth = 0.6; // Used for PlayerCollisionRange
        private const double PlayerCollisionRange = PlayerBoundingBoxWidth * 1.3;
        private const double PlayerCollisionPushStrength = 0.1;
        private const double MaxPushVelocity = 0.15;

        private const double KnockBackBaseStrength = 0.25;
        private const double KnockBackVerticalBoost = 0.4;

        private const double Epsilon = 1.0E-7;
        private const double StepHeight = 0.6;

        public async Task PhysicsTickAsync(Entity entity, Level level, Func<IServerboundPacket, Task> sendPacketDelegate, Action<Entity> updatePathFollowingInput)
        {
            // Original: if (!State.LocalPlayer.HasEntity) return; - This check should be done by the caller
            
            updatePathFollowingInput(entity);
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
                await UpdateSprintStateAndSendPackets(entity, (stoppedByCollisionX, stoppedByCollisionZ), false, sendPacketDelegate);
                return;
            }

            stoppedByCollisionX = Math.Abs(desiredDelta.X) > Epsilon && Math.Abs(actualDelta.X) < Epsilon;
            stoppedByCollisionZ = Math.Abs(desiredDelta.Z) > Epsilon && Math.Abs(actualDelta.Z) < Epsilon;

            await UpdateSprintStateAndSendPackets(entity, (stoppedByCollisionX, stoppedByCollisionZ), true, sendPacketDelegate);
        }

        private void ApplyJumpingInput(Entity entity)
        {
            if (entity is not { IsJumping: true, IsOnGround: true }) return;

            // Removed: if (_currentPath != null) block, as PathFollowerService should handle its velocity adjustments.
            // If specific logic for pathfinding jump reset is needed, it must be coordinated with PathFollowerService.

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
                if (Math.Abs(dy) > 1.0) continue; // Simplified vertical check

                var dx = entity.Position.X - otherEntity.Position.X;
                var dz = entity.Position.Z - otherEntity.Position.Z;
                var distanceSquared = dx * dx + dz * dz;

                if (distanceSquared >= PlayerCollisionRange * PlayerCollisionRange) continue;

                var pushDistance = Math.Sqrt(distanceSquared);
                if (pushDistance < 0.01) // Avoid division by zero or very small numbers
                {
                    // If entities are exactly on top of each other, push them apart in a random direction
                    var randomAngle = new Random().NextDouble() * 2 * Math.PI;
                    dx = Math.Cos(randomAngle);
                    dz = Math.Sin(randomAngle);
                    pushDistance = 0.01; // Ensure there's some distance to calculate push direction
                }

                var pushDirectionX = dx / pushDistance;
                var pushDirectionZ = dz / pushDistance;

                var pushStrength = PlayerCollisionPushStrength * (1.0 - (pushDistance / PlayerCollisionRange));

                var pushX = pushDirectionX * pushStrength;
                var pushZ = pushDirectionZ * pushStrength;

                // Clamp push velocity to avoid excessive speeds from collisions
                pushX = Math.Clamp(pushX, -MaxPushVelocity, MaxPushVelocity);
                pushZ = Math.Clamp(pushZ, -MaxPushVelocity, MaxPushVelocity);

                entity.Velocity.X += pushX;
                entity.Velocity.Z += pushZ;
            }
        }

        private void ApplyKnockBack(Entity entity)
        {
            if (!entity.IsHurt || !entity.IsHurtFromYaw.HasValue) return; // Check if IsHurtFromYaw has a value
            var lookingYaw = entity.YawPitch.X + 90; // Convert Minecraft yaw (0 south) to unit circle yaw (0 east)
            var hurtFromYaw = entity.IsHurtFromYaw.Value;

            // Calculate the direction of knockback based on entity's look direction and hurt direction
            // This logic might need adjustment based on how hurtFromYaw is defined (e.g. absolute angle or relative)
            // Assuming hurtFromYaw is an absolute angle from where the damage came
            var lookingRadians = lookingYaw * (Math.PI / 180);
            var hurtRadians = hurtFromYaw * (Math.PI / 180);
            var attackAngle = lookingRadians + hurtRadians; // This might be simpler if hurtFromYaw is relative to entity's front

            entity.Velocity.X += -Math.Sin(attackAngle) * KnockBackBaseStrength;
            entity.Velocity.Z += Math.Cos(attackAngle) * KnockBackBaseStrength;
            entity.Velocity.Y += KnockBackVerticalBoost;

            entity.IsHurtFromYaw = null; // Reset hurt state
        }

        private async Task UpdateSprintStateAndSendPackets(Entity entity, (bool collidedX, bool collidedZ) collisionFlags,
            bool sendPositionPacket, Func<IServerboundPacket, Task> sendPacketDelegate)
        {
            var wantsToMove = entity.Forward || entity.Backward || entity.Left || entity.Right;

            if (entity is { WantsToSprint: true, IsSprintingNew: false } && wantsToMove && entity.Forward &&
                collisionFlags is { collidedX: false, collidedZ: false } && entity.Hunger > 6)
            {
                await sendPacketDelegate(new PlayerCommandPacket { EntityId = entity.EntityId, Action = PlayerAction.StartSprint });
                entity.IsSprintingNew = true;
            }
            else if (entity.IsSprintingNew && (!wantsToMove || !entity.Forward || collisionFlags.collidedX || collisionFlags.collidedZ ||
                                               !entity.WantsToSprint || entity.Hunger <= 6))
            {
                await sendPacketDelegate(new PlayerCommandPacket { EntityId = entity.EntityId, Action = PlayerAction.StopSprint });
                entity.IsSprintingNew = false;
            }

            var flags = MovementFlags.None;
            if (entity.IsOnGround)
            {
                flags = MovementFlags.OnGround;
            }

            if (sendPositionPacket)
            {
                await sendPacketDelegate(new MovePlayerPositionRotationPacket
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

        private Vector3<double> MoveEntityWithCollisions(Entity entity, Level level, Vector3<double> delta)
        {
            var originalBoundingBox = entity.GetBoundingBox();
            var currentBoundingBox = originalBoundingBox;
            var wasOnGround = entity.IsOnGround;
            var landedThisTick = false;

            var expandedBox = currentBoundingBox.Expand(
                delta.X > 0 ? delta.X : 0,
                delta.Y > 0 ? delta.Y : 0,
                delta.Z > 0 ? delta.Z : 0
            ).Expand(
                delta.X < 0 ? -delta.X : 0,
                delta.Y < 0 ? -delta.Y : 0,
                delta.Z < 0 ? -delta.Z : 0
            ).Expand(Epsilon); // Expand by epsilon for safety

            var potentialColliders = level.GetCollidingBlockAABBs(expandedBox);
            var originalDeltaX = delta.X;
            var originalDeltaZ = delta.Z;

            // Y-axis
            var adjustedDeltaY = potentialColliders
                .Aggregate(delta.Y, (current, blockBox) => currentBoundingBox.CalculateYOffset(blockBox, current));
            if (Math.Abs(adjustedDeltaY - delta.Y) > Epsilon)
            {
                if (delta.Y < 0) landedThisTick = true;
                entity.Velocity.Y = 0;
            }
            currentBoundingBox = currentBoundingBox.Offset(0, adjustedDeltaY, 0);
            delta.Y = adjustedDeltaY;
            entity.IsOnGround = landedThisTick;


            // X-axis
            var adjustedDeltaX = potentialColliders
                .Aggregate(delta.X, (current, blockBox) => currentBoundingBox.CalculateXOffset(blockBox, current));
            currentBoundingBox = currentBoundingBox.Offset(adjustedDeltaX, 0, 0);
            if (Math.Abs(adjustedDeltaX - delta.X) > Epsilon) entity.Velocity.X = 0;
            delta.X = adjustedDeltaX;

            // Z-axis
            var adjustedDeltaZ = potentialColliders
                .Aggregate(delta.Z, (current, blockBox) => currentBoundingBox.CalculateZOffset(blockBox, current));
            currentBoundingBox = currentBoundingBox.Offset(0, 0, adjustedDeltaZ);
            if (Math.Abs(adjustedDeltaZ - delta.Z) > Epsilon) entity.Velocity.Z = 0;
            delta.Z = adjustedDeltaZ;

            var collidedHorizontally = (Math.Abs(delta.X - originalDeltaX) > Epsilon || Math.Abs(delta.Z - originalDeltaZ) > Epsilon);
            if (collidedHorizontally && wasOnGround && !entity.IsSneaking)
            {
                var stepAttemptDelta = PerformStepUp(level, originalBoundingBox, originalDeltaX, originalDeltaZ, potentialColliders);
                var stepSqDist = stepAttemptDelta.X * stepAttemptDelta.X + stepAttemptDelta.Z * stepAttemptDelta.Z;
                var initialSqDist = delta.X * delta.X + delta.Z * delta.Z; // Use the already resolved XZ delta

                if (stepSqDist > initialSqDist + Epsilon)
                {
                    delta = new Vector3<double>(stepAttemptDelta.X, stepAttemptDelta.Y, stepAttemptDelta.Z);
                    currentBoundingBox = originalBoundingBox.Offset(delta); // Recalculate currentBoundingBox based on step
                    entity.IsOnGround = true; // Stepping implies landing
                    entity.Velocity.Y = 0; // Stop vertical movement after step
                }
            }
            
            entity.UpdatePositionFromAABB(currentBoundingBox);
            return new Vector3<double>(delta.X, delta.Y, delta.Z);
        }

        private Vector3<double> PerformStepUp(Level level, AABB originalBox, double desiredDeltaX, double desiredDeltaZ,
            List<AABB> potentialColliders) // potentialColliders can be reused from MoveEntityWithCollisions
        {
            // --- 1. Check Vertical Clearance for Step Height ---
            var stepCheckBox = originalBox.Offset(0, StepHeight + Epsilon, 0);
            // Check if any of the *original* potential colliders (those near the initial movement path)
            // would intersect with the box *after* it's hypothetically raised by StepHeight.
            // This is a simplification; true step-up might involve checking new colliders after vertical move.
            bool canStepUp = potentialColliders.All(blockBox => !blockBox.Intersects(stepCheckBox));
            
            // More robust check: also consider blocks directly above the stepped-up position,
            // not just those that were near the initial horizontal path.
            if (canStepUp)
            {
                 // Check region from original top to stepHeight above.
                var checkRegion = originalBox.Expand(0, StepHeight, 0).Offset(0, Epsilon, 0);
                var ceilingColliders = level.GetCollidingBlockAABBs(checkRegion);
                // Ensure no blocks obstruct the space *into which* we are trying to step up.
                if (ceilingColliders.Any(blockBox => blockBox.Max.Y > originalBox.Max.Y + Epsilon && blockBox.Intersects(stepCheckBox)))
                {
                    canStepUp = false;
                }
            }

            if (!canStepUp)
            {
                return Vector3<double>.Zero; // Cannot step up
            }

            // --- 2. Perform Step Movement Sequence (Vertical, then Horizontal) ---
            // Try moving up by StepHeight, then horizontally, then back down to find support.

            var horizontalStepX = desiredDeltaX;
            var horizontalStepZ = desiredDeltaZ;

            // 2a. Move Up
            var currentStepY = StepHeight; // Attempt to move up by full step height
            var stepUpBox = originalBox; // Start from original box for this sub-movement simulation

            // Check for collisions when moving up
            var stepUpColliders = level.GetCollidingBlockAABBs(stepUpBox.Offset(0, currentStepY, 0).Expand(Epsilon));
            currentStepY = stepUpColliders.Aggregate(currentStepY, (current, blockBox) => stepUpBox.CalculateYOffset(blockBox, current));
            stepUpBox = stepUpBox.Offset(0, currentStepY, 0); // Actual vertical position after potential collision

            // 2b. Move Horizontally (X then Z) at the new height
            var currentStepX = horizontalStepX;
            var stepXColliders = level.GetCollidingBlockAABBs(stepUpBox.Offset(currentStepX, 0, 0).Expand(Epsilon));
            currentStepX = stepXColliders.Aggregate(currentStepX, (current, blockBox) => stepUpBox.CalculateXOffset(blockBox, current));
            stepUpBox = stepUpBox.Offset(currentStepX, 0, 0);

            var currentStepZ = horizontalStepZ;
            var stepZColliders = level.GetCollidingBlockAABBs(stepUpBox.Offset(0, 0, currentStepZ).Expand(Epsilon));
            currentStepZ = stepZColliders.Aggregate(currentStepZ, (current, blockBox) => stepUpBox.CalculateZOffset(blockBox, current));
            stepUpBox = stepUpBox.Offset(0, 0, currentStepZ);


            // --- 3. Settle Down onto the Step ---
            // After moving X and Z at the stepped-up height, try to move down to find solid ground.
            // The entity should not float if it stepped up partially.
            double finalDownY = 0;
            // Check down a bit more than currentStepY to ensure we find ground if it's there.
            var checkDownDist = currentStepY + 1.0; 
            var checkDownBox = stepUpBox.Offset(0, -checkDownDist, 0); // AABB for checking downwards
            var downColliders = level.GetCollidingBlockAABBs(checkDownBox);

            // Find the highest solid ground below the current stepUpBox position
            var highestGroundY = originalBox.Min.Y - 1.0; // Start below original position
            var foundGround = false;
            foreach (var blockBox in downColliders)
            {
                // Is this block below our feet and higher than previously found ground?
                if (blockBox.Max.Y <= stepUpBox.Min.Y + Epsilon && blockBox.Max.Y > highestGroundY)
                {
                    // Check for horizontal overlap
                    if (blockBox.Max.X > stepUpBox.Min.X && blockBox.Min.X < stepUpBox.Max.X &&
                        blockBox.Max.Z > stepUpBox.Min.Z && blockBox.Min.Z < stepUpBox.Max.Z)
                    {
                        highestGroundY = blockBox.Max.Y;
                        foundGround = true;
                    }
                }
            }
            
            if (foundGround)
            {
                finalDownY = highestGroundY - stepUpBox.Min.Y; // Calculate downward offset
                if (finalDownY > Epsilon) finalDownY = 0; // Don't move up if ground is somehow higher
                if (finalDownY < -checkDownDist) finalDownY = -checkDownDist; // Safety clamp
            }
            else // If no ground found after stepping, this step is invalid (e.g. stepping over a ledge)
            {
                return Vector3<double>.Zero;
            }

            stepUpBox = stepUpBox.Offset(0, finalDownY, 0);

            // --- 4. Calculate Final Delta ---
            // The final delta is the difference between the original box's min corner and the final stepUpBox's min corner.
            var finalDelta = stepUpBox.Min - originalBox.Min;

            // If the step up resulted in less horizontal movement than simply colliding, it's not a valid step.
            // (This check was originally in MoveEntityWithCollisions, makes sense here too)
            if (finalDelta.X * finalDelta.X + finalDelta.Z * finalDelta.Z < desiredDeltaX * desiredDeltaX + desiredDeltaZ * desiredDeltaZ - Epsilon &&
                Math.Abs(finalDelta.Y - currentStepY - finalDownY) < Epsilon) // Ensure Y is part of the step
            {
                 // This condition means we moved up, but then didn't move as far horizontally as intended.
                 // Could happen if the step itself is blocked.
            }


            // Ensure we actually moved up, otherwise it's not a step.
            if (finalDelta.Y < Epsilon)
            {
                return Vector3<double>.Zero;
            }
            
            return finalDelta;
        }
    }
}
