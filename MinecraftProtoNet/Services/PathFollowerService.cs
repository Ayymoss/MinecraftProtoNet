using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Handlers.Meta; // For AStarPathFinder
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;
using MinecraftProtoNet.State.Base;
using System;
using System.Collections.Generic;
using System.Linq; // Required for .Any() and .All() if used, good to have.

namespace MinecraftProtoNet.Services
{
    public class PathFollowerService : IPathFollowerService
    {
        // Copied Constants
        private const double PathNodeReachThreshold = 0.3;
        private const double PathNodeReachThresholdSq = PathNodeReachThreshold * PathNodeReachThreshold;
        private const double PathInputThreshold = 0.1;

        private const double MinSprintStraightDotProduct = 0.95;
        private const double MaxSprintVerticalChange = 1.05; // Max Y change per segment to allow sprint
        private const double MinSprintDistance = 4.0;       // Min straight distance to trigger sprint
        private const int SprintCheckLookAhead = 5;         // How many nodes to check ahead for sprint

        private const double StepHeight = 0.6; // From MinecraftClient.Physics, used here
        private const double RequiredJumpHeightThreshold = StepHeight + 0.1;
        private const double JumpAnticipationDistance = 1.0; // How close to a jump before actually jumping
        private const double JumpAnticipationDistanceSq = JumpAnticipationDistance * JumpAnticipationDistance;
        
        private const double Epsilon = 1.0E-7; // Common epsilon value

        // Logger
        private readonly ILogger<PathFollowerService> _logger = LoggingConfiguration.CreateLogger<PathFollowerService>();

        // Copied Private Fields
        private AStarPathFinder? _pathFinder;
        private List<Vector3<double>>? _currentPath;
        private int _currentPathIndex;
        private Vector3<double>? _originalTarget;
        private bool _isPartialPath;
        private float _lastMinDistance = float.MaxValue;
        
        private Level? _level;

        public void Initialize(Level level)
        {
            _level = level;
            _pathFinder = new AStarPathFinder(level);
        }

        public bool FollowPathTo(Entity entity, Vector3<double> target)
        {
            if (_pathFinder == null)
            {
                StopFollowingPath(entity);
                return false;
            }

            _originalTarget = target;
            var start = entity.Position;
            var result = _pathFinder.FindPath(start, target);

            if (result.Path is not { Count: > 1 })
            {
                StopFollowingPath(entity);
                return false;
            }

            _currentPath = result.Path;
            _isPartialPath = !result.ReachedTarget;
            _lastMinDistance = result.DistanceToTarget;
            _currentPathIndex = 1;

            return true;
        }

        public void StopFollowingPath(Entity entity)
        {
            StopFollowingPath(entity, "Requested");
        }

        private void StopFollowingPath(Entity entity, string reason)
        {
            if (_currentPath != null)
            {
                _logger.LogDebug("Pathfinding stopped. Reason: {Reason}, FinalIndex: {Index}/{Total}", 
                    reason, _currentPathIndex, _currentPath.Count);
            }

            _currentPath = null;
            _currentPathIndex = 0;
            _originalTarget = null;
            _isPartialPath = false;
            _lastMinDistance = float.MaxValue;
            
            if (entity != null)
            {
                ClearMovementInputs(entity);
            }
        }

        public void HandleTeleport(Entity entity)
        {
            if (_currentPath == null || _originalTarget == null) return;

            var newPos = entity.Position;
            
            // Check current node and next 2 nodes for proximity to the teleported position
            int bestIndex = -1;
            double minJumpSq = 4.0; // 2.0 block threshold

            for (int i = _currentPathIndex; i < Math.Min(_currentPathIndex + 3, _currentPath.Count); i++)
            {
                var node = _currentPath[i];
                var distSq = (node - newPos).LengthSquared();
                if (distSq < minJumpSq)
                {
                    minJumpSq = distSq;
                    bestIndex = i;
                }
            }

            if (bestIndex != -1)
            {
                _logger.LogInformation("Recovered pathfinding after teleport. Resuming from node {Index}. Jump distance: {Dist:F2}m", 
                    bestIndex, Math.Sqrt(minJumpSq));
                _currentPathIndex = bestIndex;
                _teleportCooldown = 10; // Wait 0.5s before moving
            }
            else
            {
                StopFollowingPath(entity, $"Hard Teleport (Too far from current path: {newPos})");
            }
        }

        private void ClearMovementInputs(Entity entity)
        {
            entity.Forward = false;
            entity.Backward = false;
            entity.Left = false;
            entity.Right = false;
            entity.StopJumping();
            entity.StopSprinting();
        }

        private int _tickCounter = 0;
        private int _teleportCooldown = 0;

        public void UpdatePathFollowingInput(Entity entity)
        {
            _tickCounter++;
            
            if (_teleportCooldown > 0)
            {
                _teleportCooldown--;
                if (_tickCounter % 5 == 0)
                {
                    _logger.LogDebug("Waiting for teleport stabilization... ({Ticks} ticks remaining)", _teleportCooldown);
                }
                ClearMovementInputs(entity);
                return;
            }

            if (_currentPath == null || _currentPathIndex >= _currentPath.Count)
            {
                // Check if we have an original target from a partial path
                var hasPartialTarget = _originalTarget != null && _isPartialPath;
                
                if (hasPartialTarget)
                {
                    // Try to recompute, but if it fails, continue toward original target anyway
                    var start = entity.Position;
                    var result = _pathFinder!.FindPath(start, _originalTarget!);

                    var isProgress = result.ReachedTarget || result.DistanceToTarget < _lastMinDistance - 0.1f;
                    var isStuckAtTip = _currentPathIndex >= (_currentPath?.Count ?? 0) && result.Path?.Count > 1;

                    if (result.Path is { Count: > 1 } && (isProgress || isStuckAtTip))
                    {
                        _currentPath = result.Path;
                        _isPartialPath = !result.ReachedTarget;
                        _lastMinDistance = result.DistanceToTarget;
                        _currentPathIndex = 1;
                        _logger.LogTrace("Recomputed path. Partial={IsPartial}, DistToTarget={Distance:F2}",
                            _isPartialPath, _lastMinDistance);
                        return;
                    }
                    
                    // Recomputation failed - but we have a target, so continue toward it directly
                    _logger.LogWarning("Path recomputation failed at end of partial path. ReachedTarget: {Reached}, Dist: {Dist:F2}", 
                        result.ReachedTarget, result.DistanceToTarget);
                    goto ContinueTowardTarget;
                }

                // No partial target - clean up
                if (_currentPath != null)
                {
                    StopFollowingPath(entity, "Path Completed");
                }
                return;
            }
            
            goto NormalPathFollowing;
            
            ContinueTowardTarget:
            {
                // Emergency fallback: move toward original target directly
                var airCurrentPos = entity.Position;
                var airTargetPos = _originalTarget!;
                var airVectorToTarget = airTargetPos - airCurrentPos;
                var airHorizontalVector = new Vector3<double>(airVectorToTarget.X, 0, airVectorToTarget.Z);
                
                if (airHorizontalVector.LengthSquared() > Epsilon * Epsilon)
                {
                    var airEyePosition = airCurrentPos + new Vector3<double>(0, Entity.PlayerEyeHeight, 0);
                    var airTargetCenter = new Vector3<double>(Math.Floor(airTargetPos.X) + 0.5, airTargetPos.Y, Math.Floor(airTargetPos.Z) + 0.5);
                    var airVectorToTargetCenter = airTargetCenter - airEyePosition;
                    var airHorizontalVectorForYaw = new Vector3<double>(airVectorToTargetCenter.X, 0, airVectorToTargetCenter.Z);
                    
                    if (airHorizontalVectorForYaw.LengthSquared() > Epsilon * Epsilon)
                    {
                        var airTargetYaw = (float)(Math.Atan2(-airHorizontalVectorForYaw.X, airHorizontalVectorForYaw.Z) * (180.0 / Math.PI));
                        entity.YawPitch = new Vector2<float>(airTargetYaw, entity.YawPitch.Y);
                    }
                    
                    var airHorizontalDirection = airHorizontalVector.Normalized();
                    var airYawRadians = entity.YawPitch.X * (Math.PI / 180.0);
                    var airSinNegativeYaw = Math.Sin(-airYawRadians);
                    var airCosNegativeYaw = Math.Cos(-airYawRadians);
                    var airLocalMoveZ = airHorizontalDirection.X * airSinNegativeYaw + airHorizontalDirection.Z * airCosNegativeYaw;
                    
                    entity.Forward = airLocalMoveZ > PathInputThreshold;
                    _logger.LogTrace("Airborne fallback: Forward={Forward}, Yaw={Yaw:F1}", entity.Forward, entity.YawPitch.X);
                }
                return;
            }
            
            NormalPathFollowing:

            var targetNode = _currentPath[_currentPathIndex];
            var currentPosition = entity.Position;

            if (_tickCounter % 20 == 0) // Log every 1 second (20 ticks)
            {
                _logger.LogDebug("Pathing Node {Index}/{Total}. Dist: {Dist:F2}m (H: {HDist:F2}m, V: {VDist:F2}m)", 
                    _currentPathIndex, _currentPath.Count, 
                    (targetNode - currentPosition).Length(),
                    Math.Sqrt(Math.Pow(targetNode.X - currentPosition.X, 2) + Math.Pow(targetNode.Z - currentPosition.Z, 2)),
                    Math.Abs(targetNode.Y - currentPosition.Y));
            }
            
            // Assuming Entity.PlayerEyeHeight is a public static const or similar.
            // If it's an instance property, it would be entity.PlayerEyeHeight
            var currentEyePosition = currentPosition + new Vector3<double>(0, Entity.PlayerEyeHeight, 0); 

            // --- Waypoint Reached Check (Adopted from Java PathNavigation) ---
            var vectorToTarget = targetNode - currentPosition;
            var horizontalVectorToTarget = new Vector3<double>(vectorToTarget.X, 0, vectorToTarget.Z);
            var horizontalDistanceToTargetSq = horizontalVectorToTarget.LengthSquared();
            
            // Java PathNavigation uses BBWidth for reach threshold:
            // maxDistanceToWaypoint = width > 0.75 ? width/2 : 0.75 - width/2
            // For Player (0.6): 0.75 - 0.3 = 0.45. We'll use 0.45.
            var reachThreshold = 0.45;
            var heightDiff = Math.Abs(targetNode.Y - currentPosition.Y);
            
            // Stricter reach for vertical transitions (jumps/drops)
            if (heightDiff > 0.1) reachThreshold = 0.2; 

            // Tighten height check for vertical transitions to prevent early completion in air
            // UP transitions (swimming/jumping) need to be very precise (0.2m)
            // DOWN transitions (dropping) can be looser (0.5m) to prevent oscillation
            var verticalReachThreshold = targetNode.Y > currentPosition.Y ? 0.2 : 0.5;
            
            bool reached = horizontalDistanceToTargetSq < (reachThreshold * reachThreshold) && heightDiff < verticalReachThreshold;

            // Strict grounding/fluid check: 
            // We cannot complete a waypoint if we are unsupported in the air (e.g., bobbing at jump apex).
            // We must either be on the ground or swimming in fluid.
            if (reached && !entity.IsOnGround && !entity.IsInWater && !entity.IsInLava)
            {
                reached = false;
            }
            
            // Java "Passed" Check: If we are close and the next node is behind us
            if (!reached && _currentPathIndex + 1 < _currentPath.Count)
            {
                var nextNode = _currentPath[_currentPathIndex + 1];
                var mobToCurrent = targetNode - currentPosition;
                var mobToNext = nextNode - currentPosition;
                var mobToCurrentHoriz = new Vector3<double>(mobToCurrent.X, 0, mobToCurrent.Z);
                var mobToNextHoriz = new Vector3<double>(mobToNext.X, 0, mobToNext.Z);
                
                if (mobToCurrentHoriz.LengthSquared() < 1.0) // Only if we are fairly close to current
                {
                    if (mobToNextHoriz.Dot(mobToCurrentHoriz) < 0)
                    {
                        reached = true; // We passed it
                        _logger.LogTrace("Passed waypoint {Index} (DotProduct check)", _currentPathIndex);
                    }
                }
            }

            if (reached)
            {
                _logger.LogTrace("Reached waypoint {Index}: {TargetNode}", _currentPathIndex, targetNode);
                _currentPathIndex++;
                if (_currentPathIndex >= _currentPath.Count)
                {
                    if (!_isPartialPath)
                    {
                        _logger.LogDebug("Path completed");
                        StopFollowingPath(entity);
                        return;
                    }
                    
                    // It was a partial path. Instead of targeting the last (now passed) waypoint,
                    // target the ORIGINAL destination to maintain forward momentum.
                    // This is critical for water-to-land transitions where we're bobbing up.
                    _logger.LogTrace("Reached tip of partial path. Targeting original destination...");
                    if (_originalTarget != null)
                    {
                        targetNode = _originalTarget;
                        vectorToTarget = targetNode - currentPosition;
                        horizontalVectorToTarget = new Vector3<double>(vectorToTarget.X, 0, vectorToTarget.Z);
                        horizontalDistanceToTargetSq = horizontalVectorToTarget.LengthSquared();
                    }
                }
                else
                {
                    // Update targetNode and related vectors for the new waypoint
                    targetNode = _currentPath[_currentPathIndex];
                    vectorToTarget = targetNode - currentPosition;
                    horizontalVectorToTarget = new Vector3<double>(vectorToTarget.X, 0, vectorToTarget.Z);
                    horizontalDistanceToTargetSq = horizontalVectorToTarget.LengthSquared();
                    _logger.LogTrace("New waypoint {Index}: {TargetNode}", _currentPathIndex, targetNode);
                }
            }

            // --- Update Look Direction (Yaw) ---
            // Target the center of the block for smoother looking
            var targetNodeCenter = new Vector3<double>(Math.Floor(targetNode.X) + 0.5, targetNode.Y, Math.Floor(targetNode.Z) + 0.5);
            var vectorToTargetCenterForYaw = targetNodeCenter - currentEyePosition;
            var horizontalVectorToTargetForYaw = new Vector3<double>(vectorToTargetCenterForYaw.X, 0, vectorToTargetCenterForYaw.Z);

            if (horizontalVectorToTargetForYaw.LengthSquared() > Epsilon * Epsilon)
            {
                var targetYaw = (float)(Math.Atan2(-horizontalVectorToTargetForYaw.X, horizontalVectorToTargetForYaw.Z) * (180.0 / Math.PI));
                entity.YawPitch = new Vector2<float>(targetYaw, entity.YawPitch.Y); // Keep current pitch
            }

            // --- Calculate Movement Input (Forward/Left/Right/Backward) ---
            // --- Calculate Movement Input (Baritone Style: Look + Forward) ---
            if (horizontalDistanceToTargetSq > Epsilon * Epsilon)
            {
                // Baritone Logic:
                // 1. Calculate ideal Yaw to target.
                // 2. Set Player Yaw to ideal Yaw.
                // 3. Set Input.Forward = true.
                // 4. No strafing (usually).

                var horizontalDirection = horizontalVectorToTarget.Normalized();
                var targetYaw = (float)(Math.Atan2(-horizontalDirection.X, horizontalDirection.Z) * (180.0 / Math.PI));
                
                // Update rotation to face target
                entity.YawPitch = new Vector2<float>(targetYaw, entity.YawPitch.Y);

                // Always move forward towards the target
                entity.Forward = true;
                entity.Backward = false;
                entity.Left = false;
                entity.Right = false; // Disable strafing to prevent diagonal collision issues

                _logger.LogTrace("Input (Baritone-style): Forward=True, Yaw={Yaw:F1}, Target={Target}", targetYaw, targetNode);

                // Sprinting Logic (Simplified)
                var shouldSprint = CalculateIfShouldSprint(entity, currentPosition, _currentPath, _currentPathIndex) && !entity.HorizontalCollision;
                if (shouldSprint)
                {
                    entity.StartSprinting();
                }
                else
                {
                    entity.StopSprinting();
                }
            }
            else
            {
                ClearMovementInputs(entity);
                entity.StopSprinting();
            }

            // --- Jump Logic (includes anticipation) ---
            var needsToJump = false;
            // Only consider jumping if on ground OR in water (to swim)
            if (entity.IsOnGround || entity.IsInWater)
            {
                var verticalDistanceToTarget = targetNode.Y - currentPosition.Y;
                
                // Check if we need to jump up a block (or are blocked by a block in front)
                // Also check if we face a block we need to jump over (AutoJump)
                // Simple heuristic: If target is higher and close, jump.
                var heightDiffNext = targetNode.Y - currentPosition.Y;
                
                // Condition for needing to jump:
                // 1. Next node is significantly higher OR
                // 2. Node after next is significantly higher AND next node isn't a drop
                
                var jumpRequiredSoon = (heightDiffNext > 0.6); // 0.6 is MaxStepHeight. If >0.6, we MUST jump.

                if (jumpRequiredSoon)
                {
                    // Baritone-like simulation:
                    // Only jump if we are actually obstructed by the block we want to climb.
                    // If we are far away, we should just walk forward until we hit it.
                    // This prevents "early jumping" where we jump 2 blocks away and fail to land on top.

                    var isCollidingWithWall = entity.HorizontalCollision;
                    var diff = new Vector2<double>(targetNode.X, targetNode.Z) - new Vector2<double>(currentPosition.X, currentPosition.Z);
                    var distToNextSq = diff.X * diff.X + diff.Y * diff.Y;
                    
                    // Allow jump if:
                    // a) We are colliding horizontally (presumably with the step)
                    // b) OR We are extremely close to the center of the target node (e.g. < 1.0m) AND the block is right there.
                    // But (a) is the most robust signal.
                    
                    if (isCollidingWithWall || distToNextSq < 1.0)
                    {
                         // Verify Head Bonk Clearance (Baritone-style)
                         if (_level != null && HeadBonkClear(entity, _level))
                         {
                             if (!entity.IsJumping) 
                                 _logger.LogDebug("Jumping! Reason: Step>0.6 ({HeightDiff:F2}) & Colliding/Close. TargetY={TargetY}", heightDiffNext, targetNode.Y);
                             needsToJump = true;
                         }
                         else
                         {
                              if (TimeProvider.System.GetLocalNow().ToUnixTimeMilliseconds() % 1000 < 50) // Throttle log
                                  _logger.LogWarning("Wanted to jump but HeadBonk blocked!");
                         }
                    }
                }
            }

            if (needsToJump)
            {
                entity.StartJumping();
            }
            else
            {
                // Stop jumping if on ground OR if in water but don't need to swim up.
                // We keep jumping if we are in the middle of a land jump (not on ground and not in water).
                if (entity.IsOnGround || entity.IsInWater) 
                {
                    entity.StopJumping();
                }
            }
        }

        private bool CalculateIfShouldSprint(Entity entity, Vector3<double> currentPosition, List<Vector3<double>> path, int currentPathIndex)
        {
            if (entity.IsSneaking) return false;
            if (!entity.IsOnGround) return false; 
            if (entity.Hunger <= 6) return false; // Minecraft hunger requirement for sprinting

            if (currentPathIndex >= path.Count) return false;

            double accumulatedStraightFlatDistance = 0;
            var lastNodePosition = currentPosition; // Start from current entity position for the first segment

            var lookAheadLimit = Math.Min(path.Count, currentPathIndex + SprintCheckLookAhead);

            for (var i = currentPathIndex; i < lookAheadLimit; i++)
            {
                var targetNode = path[i];
                var vectorToTarget = targetNode - lastNodePosition;
                var segmentDistance = Math.Sqrt(vectorToTarget.X * vectorToTarget.X + vectorToTarget.Z * vectorToTarget.Z);

                // Check 1: Vertical change for this segment
                if (Math.Abs(vectorToTarget.Y) > MaxSprintVerticalChange)
                {
                    // If this segment alone is too steep, we might still sprint up to it if previous segments were fine.
                    // The decision is whether the *path ahead* is suitable for sprinting.
                    // If the current segment is too steep, the path from here is not sprintable.
                    return accumulatedStraightFlatDistance >= MinSprintDistance; // Sprint if enough distance *before* this steep part
                }

                // Check 2: Straightness (compare direction of this segment to the next one)
                if (i + 1 < lookAheadLimit) // If there's a "next segment" to compare with
                {
                    var nextNode = path[i + 1];
                    var vectorToNext = nextNode - targetNode; // Vector from current targetNode to the one after it

                    var currentDirHorz = new Vector3<double>(vectorToTarget.X, 0, vectorToTarget.Z).Normalized();
                    var nextDirHorz = new Vector3<double>(vectorToNext.X, 0, vectorToNext.Z).Normalized();

                    if (currentDirHorz.LengthSquared() > Epsilon && nextDirHorz.LengthSquared() > Epsilon)
                    {
                        var dotProduct = currentDirHorz.Dot(nextDirHorz);
                        if (dotProduct < MinSprintStraightDotProduct) // If the path turns too sharply
                        {
                            accumulatedStraightFlatDistance += segmentDistance; // Add current segment's distance
                            // Stop here and decide based on accumulated distance so far
                            return accumulatedStraightFlatDistance >= MinSprintDistance; 
                        }
                    }
                }
                
                accumulatedStraightFlatDistance += segmentDistance;
                if (accumulatedStraightFlatDistance >= MinSprintDistance)
                {
                    return true; // Sufficient straight distance accumulated
                }

                lastNodePosition = targetNode; // For the next iteration, this node becomes the last node
            }

            // If loop finishes, check accumulated distance one last time
            return accumulatedStraightFlatDistance >= MinSprintDistance;
        }

        private bool HeadBonkClear(Entity entity, Level level)
        {
            // Check if there is enough vertical clearance to jump.
            // When jumping, we go up about 1.25 blocks.
            // We physically need to make sure we don't hit our head on a block at (Y + 2).
            // A simple check is to see if there are any collision boxes in the space we would jump into.
            
            // Expand box upwards to check for obstacles
            var jumpCheck = entity.GetBoundingBox().Offset(0, 0.6, 0); 
            // We use 0.6 because if we can't move up 0.6, we definitely can't jump full height.
            // Also, CollisionResolver uses GetCollidingBlockAABBs.
            
            var colliders = level.GetCollidingBlockAABBs(jumpCheck);
            return colliders.Count == 0;
        }
    }
}
