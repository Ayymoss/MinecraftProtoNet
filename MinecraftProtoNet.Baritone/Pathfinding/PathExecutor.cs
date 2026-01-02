using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;
using MinecraftProtoNet.State;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;

namespace MinecraftProtoNet.Baritone.Pathfinding;

/// <summary>
/// Executes a calculated path by stepping through movements.
/// Based on Baritone's PathExecutor.java.
/// </summary>
public class PathExecutor(ILogger<PathExecutor> logger, Path path, CalculationContext context)
{
    private readonly List<MovementBase> _movements = BuildMovements(path, context);
    private int _currentMovementIndex = 0;
    private int _ticksSinceLastProgress;
    private int _ticksOffPath; // Ticks spent off the expected path position
    private Models.Core.Vector3<double> _lastPrecisePosition = new();
    
    // Teleport loop detection - tracks start-of-tick position
    private Models.Core.Vector3<double>? _lastTeleportPosition;
    private int _consecutiveTeleportsToSamePosition;
    private bool _clearInputsThisTick; // Skip movement input for one tick after recovery
    
    /// <summary>
    /// Maximum times to start ticks at the same position before failing.
    /// Prevents infinite loops when server rejects movement.
    /// </summary>
    private const int MaxConsecutiveTeleports = 5;
    
    /// <summary>
    /// Distance threshold for considering two start positions as "the same place".
    /// </summary>
    private const double SamePositionThreshold = 0.05;
    
    // Baritone-style rotation handling
    private readonly Random _rotationRandom = new();
    
    /// <summary>
    /// Mouse sensitivity (0.0 to 1.0). Baritone uses minecraft's actual setting.
    /// Default 0.5 (50%) is typical for most players.
    /// </summary>
    private const double MouseSensitivity = 0.5;
    
    /// <summary>
    /// Amount of random jitter to add to rotations (Baritone's randomLooking setting).
    /// Small values make movement appear more human-like.
    /// </summary>
    private const double RandomLookingAmount = 0.5;
    
    /// <summary>
    /// Callback for placing blocks. Takes (targetX, targetY, targetZ) and returns success.
    /// </summary>
    public Func<int, int, int, bool>? OnPlaceBlockRequest { get; set; }

    /// <summary>
    /// Callback for breaking blocks. Takes (targetX, targetY, targetZ) and returns success (accepted).
    /// </summary>
    public Func<int, int, int, bool>? OnBreakBlockRequest { get; set; }

    /// <summary>
    /// Maximum ticks without progress before marking as stuck.
    /// </summary>
    private const int MaxTicksWithoutProgress = 100;
    
    /// <summary>
    /// Maximum ticks off-path before cancelling (for desync recovery).
    /// Matches Baritone's MAX_TICKS_AWAY = 200.
    /// </summary>
    private const int MaxTicksOffPath = 200;
    
    /// <summary>
    /// Distance threshold to start counting ticks off-path.
    /// Matches Baritone's MAX_DIST_FROM_PATH = 2.
    /// </summary>
    private const double MaxDistFromPath = 2.0;
    
    /// <summary>
    /// Distance threshold for immediate path cancellation (too far).
    /// Matches Baritone's MAX_MAX_DIST_FROM_PATH = 3.
    /// </summary>
    private const double MaxMaxDistFromPath = 3.0;

    /// <summary>
    /// The path being executed.
    /// </summary>
    public Path Path => path;

    /// <summary>
    /// Whether path execution has finished (success or failure).
    /// </summary>
    public bool Finished { get; private set; }

    /// <summary>
    /// Whether path execution failed.
    /// </summary>
    public bool Failed { get; private set; }
    
    /// <summary>
    /// Whether path failed specifically due to teleport loop detection.
    /// When true, the goal should be cancelled to avoid infinite retries.
    /// </summary>
    public bool FailedDueToTeleportLoop { get; private set; }

    /// <summary>
    /// Whether currently sprinting.
    /// </summary>
    public bool IsSprinting { get; private set; }

    /// <summary>
    /// Called when the server sends a teleport packet.
    /// This is the only reliable way to detect server-side movement rejection.
    /// </summary>
    public void OnServerTeleport(Models.Core.Vector3<double> teleportPosition)
    {
        if (Finished) return;
        
        if (_lastTeleportPosition != null)
        {
            var distFromLastTeleport = Math.Sqrt(
                Math.Pow(teleportPosition.X - _lastTeleportPosition.X, 2) +
                Math.Pow(teleportPosition.Y - _lastTeleportPosition.Y, 2) +
                Math.Pow(teleportPosition.Z - _lastTeleportPosition.Z, 2));
            
            if (distFromLastTeleport < SamePositionThreshold)
            {
                // Server is teleporting us to the same position - movement rejected
                _consecutiveTeleportsToSamePosition++;
                
                logger.LogDebug("[Executor] Server teleport #{Count} to same position ({X:F3}, {Y:F3}, {Z:F3})",
                    _consecutiveTeleportsToSamePosition, teleportPosition.X, teleportPosition.Y, teleportPosition.Z);
                
                if (_consecutiveTeleportsToSamePosition >= MaxConsecutiveTeleports)
                {
                    logger.LogError("[Executor] TELEPORT LOOP: Server rejected movement {Count} times to ({X:F3}, {Y:F3}, {Z:F3})",
                        _consecutiveTeleportsToSamePosition, teleportPosition.X, teleportPosition.Y, teleportPosition.Z);
                    FailedDueToTeleportLoop = true;
                }
            }
            else
            {
                // Different teleport position - normal teleport, reset counter
                _consecutiveTeleportsToSamePosition = 1;
            }
        }
        else
        {
            _consecutiveTeleportsToSamePosition = 1;
        }
        
        _lastTeleportPosition = teleportPosition;
    }

    /// <summary>
    /// Called each tick to advance path execution.
    /// Returns true if safe to cancel at this point.
    /// </summary>
    public bool OnTick(Entity entity, Level level)
    {
        if (Finished)
        {
            return true;
        }

        if (_currentMovementIndex >= _movements.Count)
        {
            // Path complete - clear all movement inputs to prevent walking into walls
            ClearEntityMovementInputs(entity);
            Finished = true;
            return true;
        }
        
        // === Clear inputs after recovery (Baritone's clearKeys behavior) ===
        // Skip one tick of movement to let server sync
        if (_clearInputsThisTick)
        {
            _clearInputsThisTick = false;
            ClearEntityMovementInputs(entity);
            return true; // Safe to cancel, we're just syncing
        }
        
        // === TELEPORT LOOP DETECTION ===
        // Detection is now event-driven - see OnServerTeleport() method.
        // When the server sends teleport packets repeatedly to the same position,
        // we increment a counter. This avoids false positives from collision-based stuck states.
        
        // Check if we've detected a teleport loop (set by OnServerTeleport callback)
        if (FailedDueToTeleportLoop)
        {
            if (!Failed) // Log only once
            {
                logger.LogError("[Executor] TELEPORT LOOP DETECTED: Server rejecting movement. Cancelling path.");
            }
            Failed = true;
            Finished = true;
            ClearEntityMovementInputs(entity);
            return true;
        }

        var currentMovement = _movements[_currentMovementIndex];
        
        // === Path Position Recovery (Baritone lines 101-143) ===
        // Check if player is at a valid position for current movement
        var feet = GetFeetPosition(entity);
        if (!currentMovement.IsValidPosition(feet.X, feet.Y, feet.Z))
        {
            // Player is not at expected position - might have been teleported by server
            
            // Scan backward through path - player might have been teleported back (lag/rubber-banding)
            for (int i = 0; i < _currentMovementIndex && i < _movements.Count; i++)
            {
                if (_movements[i].IsValidPosition(feet.X, feet.Y, feet.Z))
                {
                    logger.LogInformation("[Executor] Path recovery: Jumping back from movement {From} to {To} (teleported back)", 
                        _currentMovementIndex, i);
                    
                    // Reset movements from i to current
                    for (int j = i; j <= _currentMovementIndex; j++)
                    {
                        _movements[j].Reset();
                    }
                    _currentMovementIndex = i;
                    _ticksSinceLastProgress = 0;
                    
                    // Clear inputs for one tick (like Baritone's clearKeys)
                    _clearInputsThisTick = true;
                    ClearEntityMovementInputs(entity);
                    return true; // Safe to cancel, resuming next tick
                }
            }
            
            // If still not at valid position, scan forward - player might have skipped ahead
            if (!currentMovement.IsValidPosition(feet.X, feet.Y, feet.Z))
            {
                for (int i = _currentMovementIndex + 3; i < _movements.Count - 1; i++)
                {
                    if (_movements[i].IsValidPosition(feet.X, feet.Y, feet.Z))
                    {
                        logger.LogInformation("[Executor] Path recovery: Skipping forward from movement {From} to {To}", 
                            _currentMovementIndex, i - 1);
                        _currentMovementIndex = i - 1;
                        _ticksSinceLastProgress = 0;
                        currentMovement = _movements[_currentMovementIndex];
                        break;
                    }
                }
            }
        }
        
        // === Baritone lines 128-143: Distance-based off-path detection ===
        var closestDist = ClosestPathDistance(entity);
        
        // If possibly off-path (> 2 blocks), start counting ticks
        if (closestDist > MaxDistFromPath)
        {
            _ticksOffPath++;
            logger.LogDebug("[Executor] Off-path: distance={Dist:F2}, ticks={Ticks}/{Max}", 
                closestDist, _ticksOffPath, MaxTicksOffPath);
            
            if (_ticksOffPath > MaxTicksOffPath)
            {
                logger.LogWarning("[Executor] Too far from path for too long ({Ticks} ticks, dist={Dist:F2}), cancelling path", 
                    _ticksOffPath, closestDist);
                Failed = true;
                Finished = true;
                return true;
            }
        }
        else
        {
            _ticksOffPath = 0; // Reset off-path counter when close to path
        }
        
        // If WAY too far (> 3 blocks), cancel immediately (Baritone lines 140-143)
        if (closestDist > MaxMaxDistFromPath)
        {
            logger.LogWarning("[Executor] Way too far from path (distance={Dist:F2}), cancelling immediately", closestDist);
            Failed = true;
            Finished = true;
            return true;
        }
        
        var state = currentMovement.UpdateState(entity, level);

        // === Baritone's sprintableAscend check ===
        // If next movement is an ascend, disable sprint to allow deceleration before jump.
        // Sprint velocity causes collision with block edge during ascend.
        if (state.Sprint && _currentMovementIndex < _movements.Count - 1)
        {
            var nextMovement = _movements[_currentMovementIndex + 1];
            if (nextMovement is MovementAscend)
            {
                logger.LogDebug("[Executor] Disabling sprint before ascend at movement {Index}", _currentMovementIndex);
                state.Sprint = false;
            }
        }

        // Apply movement inputs to entity
        ApplyMovementState(entity, state);
        IsSprinting = state.Sprint;

        logger.LogDebug("[Executor] Tick {Index}/{Count} | {MovementType} | Status: {Status} | Pos: {Position} | Input: Fwd={Forward} Jmp={Jump} Spr={Sprint} Yaw={Yaw:F1} Pitch={Pitch:F1}",
            _currentMovementIndex, _movements.Count, currentMovement.GetType().Name, state.Status, 
            entity.Position, entity.Input.Forward, entity.Input.Jump, entity.Input.Sprint, entity.YawPitch.X, entity.YawPitch.Y);

        // Check movement status
        switch (state.Status)
        {
            case MovementStatus.Success:
                _currentMovementIndex++;
                _ticksSinceLastProgress = 0;
                if (_currentMovementIndex >= _movements.Count)
                {
                    Finished = true;
                }
                return currentMovement.SafeToCancel();

            case MovementStatus.Failed:
            case MovementStatus.Unreachable:
                Failed = true;
                Finished = true;
                return true;

            case MovementStatus.Running:
            case MovementStatus.Prepping:
            case MovementStatus.Waiting:
                // Check for stuck using precise distance (not just block coords)
                var currentPrecisePos = entity.Position;
                var distMoved = Math.Sqrt(
                    Math.Pow(currentPrecisePos.X - _lastPrecisePosition.X, 2) +
                    Math.Pow(currentPrecisePos.Y - _lastPrecisePosition.Y, 2) +
                    Math.Pow(currentPrecisePos.Z - _lastPrecisePosition.Z, 2));
                
                // If horizontal collision and not moving much
                if (entity.HorizontalCollision && distMoved < 0.05)
                {
                    _ticksSinceLastProgress++;
                }
                else if (distMoved < 0.01)
                {
                    // Not moving at all (within epsilon)
                    _ticksSinceLastProgress++;
                }
                else
                {
                    _ticksSinceLastProgress = 0;
                }
                _lastPrecisePosition = currentPrecisePos;
                
                if (_ticksSinceLastProgress > MaxTicksWithoutProgress)
                {
                    logger.LogWarning("[Executor] STUCK DETECTED: No progress for {Ticks} ticks at {Position}",
                        _ticksSinceLastProgress, entity.Position);
                    Failed = true;
                    Finished = true;
                    return true;
                }
                return currentMovement.SafeToCancel();

            default:
                return true;
        }
    }

    /// <summary>
    /// Gets the current movement being executed.
    /// </summary>
    public MovementBase? GetCurrentMovement()
    {
        if (_currentMovementIndex >= _movements.Count) return null;
        return _movements[_currentMovementIndex];
    }

    /// <summary>
    /// Gets progress as a percentage (0-100).
    /// </summary>
    public int GetProgressPercent()
    {
        if (_movements.Count == 0) return 100;
        return (_currentMovementIndex * 100) / _movements.Count;
    }

    /// <summary>
    /// Attempts to splice this path with a next path segment.
    /// Returns the spliced executor or this if splicing isn't possible.
    /// </summary>
    public PathExecutor TrySplice(PathExecutor? next)
    {
        // Simple implementation - just return this for now
        // Full implementation would merge paths at common points
        return this;
    }

    /// <summary>
    /// Checks if we can immediately jump to the next path.
    /// </summary>
    public bool SnipsnapIfPossible()
    {
        // Check if current movement just finished and we're at a safe point
        if (_currentMovementIndex > 0 && _currentMovementIndex < _movements.Count)
        {
            var current = _movements[_currentMovementIndex];
            return current.SafeToCancel();
        }
        return false;
    }

    private void ApplyMovementState(Entity entity, MovementState state)
    {
        // Apply rotation if targeting - Baritone-style (instant with mouse granularity)
        if (state.TargetRotation.HasValue)
        {
            var targetYaw = state.TargetRotation.Value.Yaw;
            var targetPitch = state.TargetRotation.Value.Pitch;
            
            var currentYaw = entity.YawPitch.X;
            var currentPitch = entity.YawPitch.Y;
            
            // Apply random jitter (Baritone's randomLooking feature)
            // Small offsets to appear more human-like
            targetYaw += (float)((_rotationRandom.NextDouble() - 0.5) * RandomLookingAmount);
            targetPitch += (float)((_rotationRandom.NextDouble() - 0.5) * RandomLookingAmount);
            
            // Calculate new rotation using mouse granularity (Baritone lines 292-307)
            var newYaw = CalculateMouseMove(currentYaw, targetYaw);
            var newPitch = CalculateMouseMove(currentPitch, targetPitch);
            
            // Normalize yaw to [-180, 180] and clamp pitch to [-90, 90]
            newYaw = NormalizeAngle(newYaw);
            newPitch = Math.Clamp(newPitch, -90f, 90f);
            
            entity.YawPitch = new Models.Core.Vector2<float>(newYaw, newPitch);
        }

        // Apply movement inputs
        entity.Forward = state.MoveForward;
        entity.Backward = state.MoveBackward;
        entity.Left = state.MoveLeft;
        entity.Right = state.MoveRight;

        if (state.Jump) entity.StartJumping();
        else entity.StopJumping();

        if (state.Sneak) entity.StartSneaking();
        else entity.StopSneaking();

        if (state.Sprint) entity.StartSprinting();
        else entity.StopSprinting();

        // Handle block placement
        if (state.RightClick && state.PlaceBlockTarget.HasValue)
        {
            var target = state.PlaceBlockTarget.Value;
            logger.LogDebug("[Executor] Block placement requested at ({X}, {Y}, {Z})", target.X, target.Y, target.Z);
            
            if (OnPlaceBlockRequest != null)
            {
                var success = OnPlaceBlockRequest(target.X, target.Y, target.Z);
                logger.LogDebug("[Executor] Block placement result: {Success}", success);
            }
            else
            {
                logger.LogWarning("[Executor] OnPlaceBlockRequest callback not set!");
            }
        }

        if (state.BreakBlockTarget.HasValue)
        {
            var target = state.BreakBlockTarget.Value;
            OnBreakBlockRequest?.Invoke(target.X, target.Y, target.Z);
        }
        else
        {
            // PROACTIVE INTERACTION: If current movement has no break target, look ahead (Horizon)
            ProcessHorizon(entity);
            
            // RUNTIME VERIFICATION: Check if the path is still valid
            VerifyFuturePath();
        }
    }

    /// <summary>
    /// Verifies that upcoming movements are still possible.
    /// If the world changes (block placed), we must abort.
    /// </summary>
    private void VerifyFuturePath()
    {
        // Check next 5 movements
        var verifyEnd = Math.Min(_movements.Count, _currentMovementIndex + 5);
        
        for (int i = _currentMovementIndex + 1; i < verifyEnd; i++)
        {
            var mov = _movements[i];
            var cost = mov.CalculateCost(context);
            
            if (cost >= ActionCosts.CostInf)
            {
                logger.LogWarning("[Executor] Path blocked at index {Index} ({Type}). Recalculated cost is Infinite. Aborting.", 
                    i, mov.GetType().Name);
                Failed = true;
                Finished = true;
                return;
            }
        }
    }

    /// <summary>
    /// Look ahead in the path to find blocks that can be broken early (Proactive Interaction).
    /// </summary>
    private void ProcessHorizon(Entity entity)
    {
        // Limit lookahead to 10 movements or end of path
        var horizonEnd = Math.Min(_movements.Count, _currentMovementIndex + 10);
        
        for (int i = _currentMovementIndex + 1; i < horizonEnd; i++)
        {
            var nextMov = _movements[i];
            var breakTargets = nextMov.GetBlocksToBreak(context);
            
            foreach (var target in breakTargets)
            {
                // Check if target is within reach (e.g. 4.5 blocks)
                var dist = DistanceToBlockCenter(entity.Position, target.X, target.Y, target.Z);
                if (dist <= 4.5) // Standard reach
                {
                    // Found a future block to break!
                    logger.LogTrace("[Horizon] Proactive break found at {Index}: ({X}, {Y}, {Z})", i, target.X, target.Y, target.Z);
                    OnBreakBlockRequest?.Invoke(target.X, target.Y, target.Z);
                    return; // Focus on the first executable interaction
                }
            }
        }
    }

    private static (int X, int Y, int Z) GetFeetPosition(Entity entity)
    {
        return ((int)Math.Floor(entity.Position.X),
                (int)Math.Floor(entity.Position.Y),
                (int)Math.Floor(entity.Position.Z));
    }
    
    /// <summary>
    /// Clears all movement inputs on the entity.
    /// Matches Baritone's clearKeys() behavior.
    /// </summary>
    private static void ClearEntityMovementInputs(Entity entity)
    {
        entity.Forward = false;
        entity.Backward = false;
        entity.Left = false;
        entity.Right = false;
        entity.StopJumping();
        entity.StopSprinting();
        entity.StopSneaking();
    }

    /// <summary>
    /// Builds movement instances from path positions.
    /// </summary>
    private static List<MovementBase> BuildMovements(Path path, CalculationContext context)
    {
        var movements = new List<MovementBase>();
        var positions = path.Positions;

        for (var i = 0; i < positions.Count - 1; i++)
        {
            var from = positions[i];
            var to = positions[i + 1];

            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var dz = to.Z - from.Z;

            MovementBase? movement = null;

            // Determine movement type based on offset
            if (dy == 0 && Math.Abs(dx) + Math.Abs(dz) == 1)
            {
                // Traverse (cardinal direction)
                var dir = GetDirection(dx, 0, dz);
                movement = new MovementTraverse(from.X, from.Y, from.Z, to.X, to.Z, dir);
            }
            else if (dy == 1 && Math.Abs(dx) + Math.Abs(dz) == 1)
            {
                // Ascend
                var dir = GetDirection(dx, 1, dz);
                movement = new MovementAscend(from.X, from.Y, from.Z, to.X, to.Z, dir);
            }
            else if (dy == -1 && Math.Abs(dx) + Math.Abs(dz) == 1)
            {
                // Descend
                var dir = GetDirection(dx, -1, dz);
                movement = new MovementDescend(from.X, from.Y, from.Z, to.X, to.Z, dir);
            }
            else if (Math.Abs(dx) == 1 && Math.Abs(dz) == 1 && Math.Abs(dy) <= 1)
            {
                // Diagonal (same level, ascend, or descend)
                var dir = GetDirection(dx, dy, dz);
                movement = new MovementDiagonal(from.X, from.Y, from.Z, to.X, to.Y, to.Z, dir);
            }
            else if (dx == 0 && dz == 0 && dy == 1)
            {
                // Pillar
                movement = new MovementPillar(from.X, from.Y, from.Z);
            }
            else if (dx == 0 && dz == 0 && dy == -1)
            {
                // Downward
                movement = new MovementDownward(from.X, from.Y, from.Z);
            }
            else if (dy < -1)
            {
                // Fall
                var dir = GetDirection(dx, dy, dz);
                movement = new MovementFall(from.X, from.Y, from.Z, to.X, to.Y, to.Z, dir);
            }
            else if (Math.Abs(dx) > 1 || Math.Abs(dz) > 1)
            {
                // Parkour
                var dist = Math.Max(Math.Abs(dx), Math.Abs(dz));
                var dir = GetDirection(Math.Sign(dx), dy, Math.Sign(dz));
                movement = new MovementParkour(from.X, from.Y, from.Z, to.X, to.Y, to.Z, dir, dist);
            }

            if (movement != null)
            {
                movements.Add(movement);
            }
        }

        return movements;
    }

    private static MoveDirection GetDirection(int dx, int dy, int dz)
    {
        // Match to closest MoveDirection
        if (dx == 0 && dz == -1)
        {
            return dy switch
            {
                0 => MoveDirection.TraverseNorth,
                1 => MoveDirection.AscendNorth,
                -1 => MoveDirection.DescendNorth,
                _ => MoveDirection.TraverseNorth
            };
        }
        if (dx == 0 && dz == 1)
        {
            return dy switch
            {
                0 => MoveDirection.TraverseSouth,
                1 => MoveDirection.AscendSouth,
                -1 => MoveDirection.DescendSouth,
                _ => MoveDirection.TraverseSouth
            };
        }
        if (dx == 1 && dz == 0)
        {
            return dy switch
            {
                0 => MoveDirection.TraverseEast,
                1 => MoveDirection.AscendEast,
                -1 => MoveDirection.DescendEast,
                _ => MoveDirection.TraverseEast
            };
        }
        if (dx == -1 && dz == 0)
        {
            return dy switch
            {
                0 => MoveDirection.TraverseWest,
                1 => MoveDirection.AscendWest,
                -1 => MoveDirection.DescendWest,
                _ => MoveDirection.TraverseWest
            };
        }
        if (dx == 1 && dz == -1) return MoveDirection.DiagonalNE;
        if (dx == -1 && dz == -1) return MoveDirection.DiagonalNW;
        if (dx == 1 && dz == 1) return MoveDirection.DiagonalSE;
        if (dx == -1 && dz == 1) return MoveDirection.DiagonalSW;
        if (dx == 0 && dz == 0 && dy == 1) return MoveDirection.Pillar;
        if (dx == 0 && dz == 0 && dy == -1) return MoveDirection.Downward;

        return MoveDirection.TraverseNorth; // Fallback
    }

    /// <summary>
    /// Normalizes an angle to the range [-180, 180].
    /// </summary>
    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    /// <summary>
    /// Calculates rotation change using mouse granularity (Baritone LookBehavior lines 292-296).
    /// Converts angle to mouse pixels, rounds to integer, converts back.
    /// This ensures rotation looks like realistic mouse movement.
    /// </summary>
    private static float CalculateMouseMove(float current, float target)
    {
        var delta = target - current;
        var deltaPx = AngleToMouse(delta);
        return current + MouseToAngle(deltaPx);
    }

    /// <summary>
    /// Converts an angle delta to mouse pixel movement (Baritone lines 298-300).
    /// </summary>
    private static double AngleToMouse(float angleDelta)
    {
        var minAngleChange = MouseToAngle(1);
        return Math.Round(angleDelta / minAngleChange);
    }

    /// <summary>
    /// Converts mouse pixel movement to angle change (Baritone lines 303-307).
    /// Uses Minecraft's sensitivity formula.
    /// </summary>
    private static float MouseToAngle(double mouseDelta)
    {
        // Minecraft's actual sensitivity formula
        // f = sensitivity * 0.6 + 0.2
        // angle = mouseDelta * f^3 * 8.0 * 0.15
        var f = MouseSensitivity * 0.6 + 0.2;
        return (float)(mouseDelta * f * f * f * 8.0 * 0.15);
    }

    /// <summary>
    /// Calculates the distance to the closest valid position in the path.
    /// Matches Baritone's closestPathPos method (lines 255-268).
    /// </summary>
    private double ClosestPathDistance(Entity entity)
    {
        double best = double.MaxValue;
        var playerPos = entity.Position;
        
        foreach (var movement in _movements)
        {
            // Check distance to source position (center of block)
            var srcDist = DistanceToBlockCenter(playerPos, movement.Source.X, movement.Source.Y, movement.Source.Z);
            if (srcDist < best) best = srcDist;
            
            // Check distance to destination position (center of block)
            var destDist = DistanceToBlockCenter(playerPos, movement.Destination.X, movement.Destination.Y, movement.Destination.Z);
            if (destDist < best) best = destDist;
        }
        
        return best;
    }

    /// <summary>
    /// Calculates 3D distance from player position to the center of a block.
    /// </summary>
    private static double DistanceToBlockCenter(Models.Core.Vector3<double> playerPos, int blockX, int blockY, int blockZ)
    {
        var dx = playerPos.X - (blockX + 0.5);
        var dy = playerPos.Y - blockY; // Player Y is at feet level, same as block Y
        var dz = playerPos.Z - (blockZ + 0.5);
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Checks if sprinting into an ascend is safe.
    /// Matches Baritone's sprintableAscend() logic from PathExecutor.java lines 531-565.
    /// </summary>
    /// <param name="current">Current movement (typically MovementTraverse)</param>
    /// <param name="next">Next movement (must be MovementAscend)</param>
    /// <param name="level">World level for block checks</param>
    /// <returns>True if safe to sprint into the ascend, false otherwise</returns>
    internal bool IsSprintableAscend(MovementBase current, MovementBase next, Level level)
    {
        if (next is not MovementAscend ascend) return false;
        
        // Traverse/Diagonal into Ascend - check direction alignment
        var currentDirX = current.Destination.X - current.Source.X;
        var currentDirZ = current.Destination.Z - current.Source.Z;
        var nextDirX = ascend.Destination.X - ascend.Source.X;
        var nextDirZ = ascend.Destination.Z - ascend.Source.Z;
        
        // Baritone line 535: current.getDirection().equals(next.getDirection().below())
        // The "below" means ascend direction projected to horizontal plane
        if (currentDirX != nextDirX || currentDirZ != nextDirZ)
        {
            logger.LogTrace("[SprintCheck] Direction mismatch: current=({Cx},{Cz}), next=({Nx},{Nz})", 
                currentDirX, currentDirZ, nextDirX, nextDirZ);
            return false;
        }
        
        // Baritone lines 541-546: Check floor walkability
        var currentFloor = level.GetBlockAt(current.Destination.X, current.Destination.Y - 1, current.Destination.Z);
        if (!MovementHelper.CanWalkOn(currentFloor))
        {
            logger.LogTrace("[SprintCheck] Can't walk on current dest floor");
            return false;
        }
        
        var nextFloor = level.GetBlockAt(ascend.Destination.X, ascend.Destination.Y - 1, ascend.Destination.Z);
        if (!MovementHelper.CanWalkOn(nextFloor))
        {
            logger.LogTrace("[SprintCheck] Can't walk on next dest floor");
            return false;
        }
        
        // Baritone lines 550-559: Check 2x3 body clearance
        // The player needs clear passage from source through the entire ascend trajectory
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                var checkX = current.Source.X + (x == 1 ? currentDirX : 0);
                var checkY = current.Source.Y + y;
                var checkZ = current.Source.Z + (x == 1 ? currentDirZ : 0);
                var block = level.GetBlockAt(checkX, checkY, checkZ);
                
                if (!MovementHelper.CanWalkThrough(block))
                {
                    logger.LogTrace("[SprintCheck] Body clearance blocked at ({X},{Y},{Z}): {Block}", 
                        checkX, checkY, checkZ, block?.Name ?? "null");
                    return false;
                }
            }
        }
        
        // Baritone lines 561-564: Check for hazards above
        var aboveHead = level.GetBlockAt(current.Source.X, current.Source.Y + 3, current.Source.Z);
        if (aboveHead != null && MovementHelper.AvoidWalkingInto(aboveHead))
        {
            logger.LogTrace("[SprintCheck] Hazard above head: {Block}", aboveHead.Name);
            return false;
        }
        
        var aboveDestHead = level.GetBlockAt(ascend.Destination.X, ascend.Destination.Y + 2, ascend.Destination.Z);
        if (aboveDestHead != null && MovementHelper.AvoidWalkingInto(aboveDestHead))
        {
            logger.LogTrace("[SprintCheck] Hazard above dest head: {Block}", aboveDestHead.Name);
            return false;
        }
        
        logger.LogTrace("[SprintCheck] Sprint ascend is SAFE");
        return true;
    }
}
