using MinecraftProtoNet.Handlers.Meta;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Core;


// TODO: Support any coordinates by looping for smaller sections. How do we handle Y? Closest block?
public partial class MinecraftClient
{
    private AStarPathFinder? _pathFinder;
    private List<Vector3<double>>? _currentPath;
    private int _currentPathIndex;

    private const double PathNodeReachThreshold = 0.3;
    private const double PathNodeReachThresholdSq = PathNodeReachThreshold * PathNodeReachThreshold;
    private const double PathInputThreshold = 0.1;

    private const double MinSprintStraightDotProduct = 0.95;
    private const double MaxSprintVerticalChange = 1.05;
    private const double MinSprintDistance = 4.0;
    private const int SprintCheckLookAhead = 5;

    private const double RequiredJumpHeightThreshold = StepHeight + 0.1;
    private const double JumpAnticipationDistance = 1;
    private const double JumpAnticipationDistanceSq = JumpAnticipationDistance * JumpAnticipationDistance;

    public void InitializePathFinder()
    {
        _pathFinder = new AStarPathFinder(State.Level);
    }

    /// <summary>
    /// Attempts to find a path to the target and starts following it.
    /// </summary>
    /// <param name="target">The target world coordinates.</param>
    /// <returns>True if a path was found and following started, false otherwise.</returns>
    public bool FollowPathTo(Vector3<double> target)
    {
        if (_pathFinder == null)
        {
            StopFollowingPath();
            return false;
        }

        if (!State.LocalPlayer.HasEntity)
        {
            StopFollowingPath();
            return false;
        }

        var entity = State.LocalPlayer.Entity;
        var start = entity.Position;

        _currentPath = _pathFinder.FindPath(start, target);

        if (_currentPath is not { Count: > 1 })
        {
            _currentPath = null;
            _currentPathIndex = 0;
            ClearMovementInputs(entity);
            return false;
        }

        _currentPathIndex = 1;
        return true;
    }

    /// <summary>
    /// Stops the client from following the current path and clears movement inputs.
    /// </summary>
    public void StopFollowingPath()
    {
        _currentPath = null;
        _currentPathIndex = 0;
        if (State.LocalPlayer.HasEntity)
        {
            ClearMovementInputs(State.LocalPlayer.Entity);
        }
    }

    /// <summary>
    /// Helper method to reset movement input flags on the entity.
    /// </summary>
    private void ClearMovementInputs(Entity entity)
    {
        entity.Forward = false;
        entity.Backward = false;
        entity.Left = false;
        entity.Right = false;
        entity.StopJumping();
        entity.StopSprinting();
    }

    /// <summary>
    /// Updates the entity's movement inputs (Forward, Left, Jump, Yaw) based on the current path.
    /// Should be called at the beginning of PhysicsTickAsync.
    /// </summary>
    private void UpdatePathFollowingInput(Entity entity)
    {
        if (_currentPath == null || _currentPathIndex >= _currentPath.Count)
        {
            if (_currentPath != null)
            {
                StopFollowingPath();
            }

            return;
        }

        var targetNode = _currentPath[_currentPathIndex];
        var currentPosition = entity.Position;
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
                StopFollowingPath();
                return;
            }

            targetNode = _currentPath[_currentPathIndex];
            vectorToTarget = targetNode - currentPosition;
            horizontalVectorToTarget = new Vector3<double>(vectorToTarget.X, 0, vectorToTarget.Z);
            horizontalDistanceToTargetSq = horizontalVectorToTarget.LengthSquared();
        }

        // --- Update Look Direction (Yaw) ---
        var targetNodeCenter = new Vector3<double>(Math.Floor(targetNode.X) + 0.5, targetNode.Y, Math.Floor(targetNode.Z) + 0.5);
        var vectorToTargetCenterForYaw = targetNodeCenter - currentEyePosition;
        var horizontalVectorToTargetForYaw = new Vector3<double>(vectorToTargetCenterForYaw.X, 0, vectorToTargetCenterForYaw.Z);

        if (horizontalVectorToTargetForYaw.LengthSquared() > Epsilon * Epsilon)
        {
            var targetYaw = (float)(Math.Atan2(-horizontalVectorToTargetForYaw.X, horizontalVectorToTargetForYaw.Z) * (180.0 / Math.PI));
            entity.YawPitch = new Vector2<float>(targetYaw, entity.YawPitch.Y);
        }

        // --- Calculate Movement Input (Forward/Left/Right/Backward) ---
        if (horizontalDistanceToTargetSq > Epsilon * Epsilon)
        {
            var horizontalDirection = horizontalVectorToTarget.Normalized();
            var yawRadians = entity.YawPitch.X * (Math.PI / 180.0);
            var sinNegativeYaw = Math.Sin(-yawRadians);
            var cosNegativeYaw = Math.Cos(-yawRadians);
            var localMoveX = horizontalDirection.X * cosNegativeYaw - horizontalDirection.Z * sinNegativeYaw;
            var localMoveZ = horizontalDirection.X * sinNegativeYaw + horizontalDirection.Z * cosNegativeYaw;

            entity.Forward = localMoveZ > PathInputThreshold;
            entity.Backward = localMoveZ < -PathInputThreshold;
            entity.Left = localMoveX < -PathInputThreshold;
            entity.Right = localMoveX > PathInputThreshold;

            var shouldSprint = CalculateIfShouldSprint(entity, currentPosition, _currentPath, _currentPathIndex);
            if (shouldSprint && entity.Forward)
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
            entity.Forward = false;
            entity.Backward = false;
            entity.Left = false;
            entity.Right = false;
            entity.StopSprinting();
        }

        // --- Jump Apprehension Logic ---
        var needsToJump = false;
        if (entity.IsOnGround)
        {
            var heightDiffNext = targetNode.Y - currentPosition.Y;

            var heightDiffNextNextActual = 0d;
            var hasNextNextNode = _currentPathIndex + 1 < _currentPath.Count;
            if (hasNextNextNode)
            {
                heightDiffNextNextActual = _currentPath[_currentPathIndex + 1].Y - currentPosition.Y;
            }

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
            if (entity.IsOnGround)
            {
                entity.StopJumping();
            }
        }
    }

    /// <summary>
    /// Determines if the entity should attempt to sprint based on the upcoming path geometry.
    /// </summary>
    /// <param name="entity">The entity following the path.</param>
    /// <param name="currentPosition">The current position of the entity.</param>
    /// <param name="path">The list of path nodes.</param>
    /// <param name="currentPathIndex">The index of the next node the entity is heading towards.</param>
    /// <returns>True if sprinting is recommended, false otherwise.</returns>
    private bool CalculateIfShouldSprint(Entity entity, Vector3<double> currentPosition, List<Vector3<double>> path, int currentPathIndex)
    {
        if (entity.IsSneaking) return false;
        if (!entity.IsOnGround) return false;
        if (entity.Hunger <= 6) return false;

        if (currentPathIndex >= path.Count) return false;

        double accumulatedStraightFlatDistance = 0;
        var lastNodePosition = currentPosition;

        var lookAheadLimit = Math.Min(path.Count, currentPathIndex + SprintCheckLookAhead);

        for (var i = currentPathIndex; i < lookAheadLimit; i++)
        {
            var targetNode = path[i];
            var vectorToTarget = targetNode - lastNodePosition;
            var segmentDistance = Math.Sqrt(vectorToTarget.X * vectorToTarget.X + vectorToTarget.Z * vectorToTarget.Z);

            // Check 1: Vertical change for this segment
            if (Math.Abs(vectorToTarget.Y) > MaxSprintVerticalChange)
            {
                return false;
            }

            // Check 2: Straightness (compare direction of this segment to the next one)
            if (i + 1 < lookAheadLimit)
            {
                var nextNode = path[i + 1];
                var vectorToNext = nextNode - targetNode;

                var currentDirHorz = new Vector3<double>(vectorToTarget.X, 0, vectorToTarget.Z).Normalized();
                var nextDirHorz = new Vector3<double>(vectorToNext.X, 0, vectorToNext.Z).Normalized();

                if (currentDirHorz.LengthSquared() > Epsilon && nextDirHorz.LengthSquared() > Epsilon)
                {
                    var dotProduct = currentDirHorz.Dot(nextDirHorz);
                    if (dotProduct < MinSprintStraightDotProduct)
                    {
                        accumulatedStraightFlatDistance += segmentDistance;
                        return accumulatedStraightFlatDistance >= MinSprintDistance;
                    }
                }
            }

            accumulatedStraightFlatDistance += segmentDistance;
            if (accumulatedStraightFlatDistance >= MinSprintDistance)
            {
                return true;
            }

            lastNodePosition = targetNode;
        }

        return accumulatedStraightFlatDistance >= MinSprintDistance;
    }
}
