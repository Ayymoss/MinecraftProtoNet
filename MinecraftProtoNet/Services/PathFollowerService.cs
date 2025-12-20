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

        // Copied Private Fields
        private AStarPathFinder? _pathFinder;
        private List<Vector3<double>>? _currentPath;
        private int _currentPathIndex;
        private Vector3<double>? _originalTarget;
        private bool _isPartialPath;
        private float _lastMinDistance = float.MaxValue;

        public void Initialize(Level level)
        {
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
            _currentPath = null;
            _currentPathIndex = 0;
            _originalTarget = null;
            _isPartialPath = false;
            _lastMinDistance = float.MaxValue;
            // entity is passed as parameter, no need for State.LocalPlayer.HasEntity
            if (entity != null) // Ensure entity is not null before clearing inputs
            {
                ClearMovementInputs(entity);
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

        public void UpdatePathFollowingInput(Entity entity)
        {
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
                        Console.WriteLine($"[PATH_DEBUG] Recomputed path. Partial={_isPartialPath}, DistToTarget={_lastMinDistance:F2}");
                        return;
                    }
                    
                    // Recomputation failed - but we have a target, so continue toward it directly
                    Console.WriteLine("[PATH_DEBUG] Recomputation failed - continuing toward original target directly.");
                    goto ContinueTowardTarget;
                }

                // No partial target - clean up
                if (_currentPath != null)
                {
                    StopFollowingPath(entity);
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
                    Console.WriteLine($"[PATH_DEBUG] Airborne fallback: FWD={entity.Forward}, Yaw={entity.YawPitch.X:F1}");
                }
                return;
            }
            
            NormalPathFollowing:

            var targetNode = _currentPath[_currentPathIndex];
            var currentPosition = entity.Position;
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
                        Console.WriteLine($"[PATH_DEBUG] Passed waypoint {_currentPathIndex} (DotProduct check)");
                    }
                }
            }

            if (reached)
            {
                Console.WriteLine($"[PATH_DEBUG] Reached waypoint {_currentPathIndex}: {targetNode}");
                _currentPathIndex++;
                if (_currentPathIndex >= _currentPath.Count)
                {
                    if (!_isPartialPath)
                    {
                        Console.WriteLine("[PATH_DEBUG] Path completed.");
                        StopFollowingPath(entity);
                        return;
                    }
                    
                    // It was a partial path. Instead of targeting the last (now passed) waypoint,
                    // target the ORIGINAL destination to maintain forward momentum.
                    // This is critical for water-to-land transitions where we're bobbing up.
                    Console.WriteLine("[PATH_DEBUG] Reached tip of partial path. Targeting original destination...");
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
                    Console.WriteLine($"[PATH_DEBUG] New waypoint {_currentPathIndex}: {targetNode}");
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
            if (horizontalDistanceToTargetSq > Epsilon * Epsilon) // If not already at the target node horizontally
            {
                var horizontalDirection = horizontalVectorToTarget.Normalized();
                var yawRadians = entity.YawPitch.X * (Math.PI / 180.0);
                // Rotate movement vector to be local to player's orientation
                var sinNegativeYaw = Math.Sin(-yawRadians); 
                var cosNegativeYaw = Math.Cos(-yawRadians);
                var localMoveX = horizontalDirection.X * cosNegativeYaw - horizontalDirection.Z * sinNegativeYaw;
                var localMoveZ = horizontalDirection.X * sinNegativeYaw + horizontalDirection.Z * cosNegativeYaw;

                entity.Forward = localMoveZ > PathInputThreshold;
                entity.Backward = localMoveZ < -PathInputThreshold;
                entity.Left = localMoveX < -PathInputThreshold; // Corrected: was localMoveX > PathInputThreshold for Left
                entity.Right = localMoveX > PathInputThreshold;

                Console.WriteLine($"[PATH_DEBUG] Input: FWD={entity.Forward}, BCK={entity.Backward}, LFT={entity.Left}, RGT={entity.Right}, SPRINT={entity.IsSprinting}, Yaw={entity.YawPitch.X:F1}");

                var shouldSprint = CalculateIfShouldSprint(entity, currentPosition, _currentPath, _currentPathIndex);
                if (shouldSprint && entity.Forward) // Only sprint if moving forward
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
                ClearMovementInputs(entity); // At destination node, clear inputs (except jump if needed)
                entity.StopSprinting();
            }

            // --- Jump Logic (includes anticipation) ---
            var needsToJump = false;
            // Only consider jumping if on ground OR in water (to swim)
            if (entity.IsOnGround || entity.IsInWater) 
            {
                // Check height difference to the immediate next node
                var heightDiffNext = targetNode.Y - currentPosition.Y;

                // Check height difference to the node after next, if it exists
                var heightDiffNextNextActual = 0.0;
                var hasNextNextNode = _currentPathIndex + 1 < _currentPath.Count;
                if (hasNextNextNode)
                {
                    heightDiffNextNextActual = _currentPath[_currentPathIndex + 1].Y - currentPosition.Y;
                }
                
                // Condition for needing to jump:
                // 1. Next node is significantly higher OR
                // 2. Node after next is significantly higher AND next node isn't a drop
                // 3. Swimming up: Target node is above us and we are in water
                // 4. Stuck against a wall (HorizontalCollision), but NOT if we are dropping
                var jumpRequiredSoon = (heightDiffNext > RequiredJumpHeightThreshold) ||
                                       (hasNextNextNode && heightDiffNextNextActual > RequiredJumpHeightThreshold && heightDiffNext > -0.2) ||
                                       (entity.IsInWater && heightDiffNext > 0.1) ||
                                       (entity.HorizontalCollision && heightDiffNext >= -0.2); // Don't jump if dropping significantly

                if (jumpRequiredSoon && (horizontalDistanceToTargetSq < JumpAnticipationDistanceSq || entity.HorizontalCollision))
                {
                    needsToJump = true;
                    if (entity.IsInWater && heightDiffNext > 0.1) Console.WriteLine($"[PATH_DEBUG] Decided to swim up: TargetY={targetNode.Y:F2}, CurrentY={currentPosition.Y:F2}");
                    else if (entity.HorizontalCollision && heightDiffNext < RequiredJumpHeightThreshold) Console.WriteLine("[PATH_DEBUG] Jumping due to horizontal collision.");
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
    }
}
