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

        public void Initialize(Level level)
        {
            _pathFinder = new AStarPathFinder(level);
        }

        public bool FollowPathTo(Entity entity, Vector3<double> target)
        {
            if (_pathFinder == null)
            {
                StopFollowingPath(entity); // Pass entity
                return false;
            }

            // entity is already available as a parameter, no need for State.LocalPlayer.HasEntity
            // if (entity == null) { StopFollowingPath(entity); return false; } // This check might be redundant if entity is guaranteed non-null

            var start = entity.Position;
            _currentPath = _pathFinder.FindPath(start, target);

            if (_currentPath is not { Count: > 1 }) // Path needs at least start and one target node
            {
                _currentPath = null;
                _currentPathIndex = 0;
                ClearMovementInputs(entity);
                return false;
            }

            _currentPathIndex = 1; // Start by aiming for the second node in the path (index 1)
            return true;
        }

        public void StopFollowingPath(Entity entity)
        {
            _currentPath = null;
            _currentPathIndex = 0;
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
                if (_currentPath != null) // If there was a path but it's now completed/invalid
                {
                    StopFollowingPath(entity);
                }
                return;
            }

            var targetNode = _currentPath[_currentPathIndex];
            var currentPosition = entity.Position;
            // Assuming Entity.PlayerEyeHeight is a public static const or similar.
            // If it's an instance property, it would be entity.PlayerEyeHeight
            var currentEyePosition = currentPosition + new Vector3<double>(0, Entity.PlayerEyeHeight, 0); 

            // --- Waypoint Reached Check ---
            var vectorToTarget = targetNode - currentPosition;
            var horizontalVectorToTarget = new Vector3<double>(vectorToTarget.X, 0, vectorToTarget.Z);
            var horizontalDistanceToTargetSq = horizontalVectorToTarget.LengthSquared();

            if (horizontalDistanceToTargetSq < PathNodeReachThresholdSq)
            {
                _currentPathIndex++;
                if (_currentPathIndex >= _currentPath.Count)
                {
                    StopFollowingPath(entity);
                    return;
                }
                // Update targetNode and related vectors for the new waypoint
                targetNode = _currentPath[_currentPathIndex];
                vectorToTarget = targetNode - currentPosition;
                horizontalVectorToTarget = new Vector3<double>(vectorToTarget.X, 0, vectorToTarget.Z);
                horizontalDistanceToTargetSq = horizontalVectorToTarget.LengthSquared();
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
            if (entity.IsOnGround) // Only consider jumping if on ground
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
                var jumpRequiredSoon = (heightDiffNext > RequiredJumpHeightThreshold) ||
                                       (hasNextNextNode && heightDiffNextNextActual > RequiredJumpHeightThreshold && heightDiffNext > -0.2);

                if (jumpRequiredSoon && horizontalDistanceToTargetSq < JumpAnticipationDistanceSq)
                {
                    needsToJump = true;
                }
            }

            if (needsToJump)
            {
                entity.StartJumping();
            }
            else
            {
                // Stop jumping only if on ground and no jump is immediately needed.
                // This prevents interrupting an ongoing jump if IsOnGround becomes true mid-air.
                if (entity.IsOnGround) 
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
