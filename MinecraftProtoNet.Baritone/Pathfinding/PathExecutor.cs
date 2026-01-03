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
    private readonly CalculationContext _context = context;
    private readonly List<MovementBase> _movements = BuildMovements(path, context);
    private int _currentMovementIndex = 0;
    private int _ticksSinceLastProgress;
    private int _ticksOffPath; // Ticks spent off the expected path position
    private Models.Core.Vector3<double> _lastPrecisePosition = new();
    
    // Teleport loop detection - tracks start-of-tick position
    private Models.Core.Vector3<double>? _lastTeleportPosition;
    private int _consecutiveTeleportsToSamePosition;
    private bool _clearInputsThisTick; // Skip movement input for one tick after recovery
    
    // Block tracking for proactive interaction
    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:70-72
    private HashSet<(int X, int Y, int Z)> _toBreak = new();
    private HashSet<(int X, int Y, int Z)> _toPlace = new();
    private HashSet<(int X, int Y, int Z)> _toWalkInto = new();
    private bool _recalcBP = true; // Recalculate block tracking flag
    
    // Cost tracking for movement timeout and verification
    // Reference: PathExecutor.java:65-67
    private double? _currentMovementOriginalCostEstimate;
    private int? _costEstimateIndex;
    private int _ticksOnCurrent;
    
    // Sprint state tracking
    private bool _sprintNextTick;
    
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
    /// Callback to get the best path so far from an in-progress pathfinder (for pause logic).
    /// Returns null if no path calculation is in progress or no valid partial path exists.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:270-301
    /// </summary>
    public Func<Path?>? GetBestPathSoFar { get; set; }

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
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:303-316
        // Special handling for MovementFall: use flat distance (ignore Y) when falling
        bool possiblyOffPath = false;
        if (closestDist > MaxDistFromPath)
        {
            if (currentMovement is MovementFall)
            {
                // During a fall, we're midair and far from both start and end
                // Check flat distance to fall destination instead
                var fallDest = currentMovement.Destination;
                var flatDist = Math.Sqrt(
                    Math.Pow(entity.Position.X - (fallDest.X + 0.5), 2) +
                    Math.Pow(entity.Position.Z - (fallDest.Z + 0.5), 2));
                possiblyOffPath = flatDist >= MaxDistFromPath;
            }
            else
            {
                possiblyOffPath = true;
            }
        }
        
        if (possiblyOffPath)
        {
            _ticksOffPath++;
            logger.LogDebug("[Executor] Off-path: distance={Dist:F2}, ticks={Ticks}/{Max}", 
                closestDist, _ticksOffPath, MaxTicksOffPath);
            
            if (_ticksOffPath > MaxTicksOffPath)
            {
                logger.LogWarning("[Executor] Too far from path for too long ({Ticks} ticks, dist={Dist:F2}), cancelling path", 
                    _ticksOffPath, closestDist);
                ClearEntityMovementInputs(entity); // Baritone: cancel() calls clearKeys()
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
        // Note: MovementFall special handling doesn't apply here - if we're > 3 blocks away, cancel
        if (closestDist > MaxMaxDistFromPath)
        {
            logger.LogWarning("[Executor] Way too far from path (distance={Dist:F2}), cancelling immediately", closestDist);
            ClearEntityMovementInputs(entity); // Baritone: cancel() calls clearKeys()
            Failed = true;
            Finished = true;
            return true;
        }
        
        // === Block Tracking Recalculation (Baritone lines 146-180) ===
        // Check movements around current position for changes
        for (int i = _currentMovementIndex - 10; i < _currentMovementIndex + 10; i++)
        {
            if (i < 0 || i >= _movements.Count) continue;
            
            var mov = _movements[i];
            var prevBreak = new HashSet<(int X, int Y, int Z)>(mov.GetBlocksToBreak(context));
            var prevPlace = new HashSet<(int X, int Y, int Z)>(mov.GetBlocksToPlace(context));
            var prevWalkInto = new HashSet<(int X, int Y, int Z)>(mov.GetBlocksToWalkInto(context));
            
            // Reset movement cache - movements may cache block lists
            mov.Reset();
            
            var newBreak = new HashSet<(int X, int Y, int Z)>(mov.GetBlocksToBreak(context));
            var newPlace = new HashSet<(int X, int Y, int Z)>(mov.GetBlocksToPlace(context));
            var newWalkInto = new HashSet<(int X, int Y, int Z)>(mov.GetBlocksToWalkInto(context));
            
            if (!prevBreak.SetEquals(newBreak) || !prevPlace.SetEquals(newPlace) || !prevWalkInto.SetEquals(newWalkInto))
            {
                _recalcBP = true;
                break; // Changes detected, will recalculate below
            }
        }
        
        // Recalculate all blocks from current position forward if needed
        if (_recalcBP)
        {
            var newBreak = new HashSet<(int X, int Y, int Z)>();
            var newPlace = new HashSet<(int X, int Y, int Z)>();
            var newWalkInto = new HashSet<(int X, int Y, int Z)>();
            
            for (int i = _currentMovementIndex; i < _movements.Count; i++)
            {
                var mov = _movements[i];
                foreach (var block in mov.GetBlocksToBreak(context))
                {
                    newBreak.Add(block);
                }
                foreach (var block in mov.GetBlocksToPlace(context))
                {
                    newPlace.Add(block);
                }
                foreach (var block in mov.GetBlocksToWalkInto(context))
                {
                    newWalkInto.Add(block);
                }
            }
            
            _toBreak = newBreak;
            _toPlace = newPlace;
            _toWalkInto = newWalkInto;
            _recalcBP = false;
        }
        
        // === Chunk Loading Check (Baritone lines 185-192) ===
        // Pause if next movement destination is at edge of loaded chunks
        if (_currentMovementIndex < _movements.Count - 1)
        {
            var nextMovement = _movements[_currentMovementIndex + 1];
            if (!context.IsLoaded(nextMovement.Destination.X, nextMovement.Destination.Z))
            {
                logger.LogDebug("[Executor] Pausing since destination is at edge of loaded chunks");
                ClearEntityMovementInputs(entity);
                return true; // Safe to cancel
            }
        }
        
        // === Cost Verification Lookahead (Baritone lines 194-218) ===
        bool canCancel = currentMovement.SafeToCancel();
        if (_costEstimateIndex == null || _costEstimateIndex != _currentMovementIndex)
        {
            _costEstimateIndex = _currentMovementIndex;
            // Cache the original cost estimate for this movement
            _currentMovementOriginalCostEstimate = currentMovement.Cost;
            
            // Verify future movements are still possible (lookahead)
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:198-204
            // Only check once when movement starts, not every tick
            // IMPORTANT: We must reset movements before recalculating cost, as they cache block states
            // Use the original context (Java Baritone's secretInternalGetCalculationContext returns the same context)
            // The context's Level reference should point to the current world state
            const int costVerificationLookahead = 5; // Baritone default
            for (int i = 1; i < costVerificationLookahead && _currentMovementIndex + i < _movements.Count - 1; i++)
            {
                var futureMov = _movements[_currentMovementIndex + i];
                
                // Reset movement to clear any cached state before recalculating
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Movement.java:254-260
                futureMov.Reset();
                
                var futureCost = futureMov.CalculateCost(_context);
                if (futureCost >= ActionCosts.CostInf && canCancel)
                {
                    // Log detailed debug info to understand why movement is impossible
                    logger.LogDebug("[Executor] Future movement {Type} from ({SrcX}, {SrcY}, {SrcZ}) to ({DestX}, {DestY}, {DestZ}) failed. " +
                        "HasThrowaway={HasThrowaway}, AllowPlace={AllowPlace}, AllowBreak={AllowBreak}",
                        futureMov.GetType().Name,
                        futureMov.Source.X, futureMov.Source.Y, futureMov.Source.Z,
                        futureMov.Destination.X, futureMov.Destination.Y, futureMov.Destination.Z,
                        _context.HasThrowaway, _context.AllowPlace, _context.AllowBreak);
                    
                    logger.LogWarning("[Executor] Future movement at index {Index} ({Type}) from ({SrcX}, {SrcY}, {SrcZ}) to ({DestX}, {DestY}, {DestZ}) has become impossible (cost={Cost}). Cancelling path.", 
                        _currentMovementIndex + i, futureMov.GetType().Name, 
                        futureMov.Source.X, futureMov.Source.Y, futureMov.Source.Z,
                        futureMov.Destination.X, futureMov.Destination.Y, futureMov.Destination.Z,
                        futureCost);
                    ClearEntityMovementInputs(entity);
                    Failed = true;
                    Finished = true;
                    return true;
                }
            }
        }
        
        // Recalculate current movement cost to check if it's still possible
        // Use the original context (Java Baritone's secretInternalGetCalculationContext returns the same context)
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:206
        currentMovement.Reset();
        var currentCost = currentMovement.CalculateCost(_context);
        if (currentCost >= ActionCosts.CostInf && canCancel)
        {
            logger.LogWarning("[Executor] Current movement has become impossible. Cancelling path.");
            ClearEntityMovementInputs(entity);
            Failed = true;
            Finished = true;
            return true;
        }
        
        // Max cost increase check (Baritone lines 212-218)
        // Prevents cache errors where a movement becomes much more expensive due to world changes
        // Only check if movement wasn't calculated while chunk was loaded (to avoid false positives)
        const double maxCostIncrease = 10.0; // Baritone default setting (Settings.java line 516)
        if (_currentMovementOriginalCostEstimate.HasValue && 
            currentCost - _currentMovementOriginalCostEstimate.Value > maxCostIncrease && 
            canCancel)
        {
            logger.LogDebug("[Executor] Original cost {OriginalCost} current cost {CurrentCost}. Cancelling due to large cost increase.",
                _currentMovementOriginalCostEstimate.Value, currentCost);
            ClearEntityMovementInputs(entity);
            Failed = true;
            Finished = true;
            return true;
        }
        
        // === PAUSE CHECK (Baritone lines 219-223) ===
        // Pause if we're backtracking and a new path is being calculated
        if (ShouldPause(entity, level))
        {
            logger.LogDebug("[Executor] Pausing execution - player is backtracking on new path");
            ClearEntityMovementInputs(entity);
            return true; // Safe to cancel, will resume when new path is ready
        }
        
        var state = currentMovement.UpdateState(entity, level);

        // === Sprint Logic (Baritone shouldSprintNextTick) ===
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:344-475
        bool shouldSprint = ShouldSprintNextTick(entity, level, currentMovement, state);
        state.Sprint = shouldSprint;

        // Apply movement inputs to entity
        ApplyMovementState(entity, state, currentMovement);
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
                OnChangeInPathPosition(entity);
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
                // Increment ticks on current movement (Baritone line 241)
                _ticksOnCurrent++;
                
                // Check for movement timeout (Baritone lines 242-250)
                const int movementTimeoutTicks = 100; // Baritone default
                if (_currentMovementOriginalCostEstimate.HasValue && 
                    _ticksOnCurrent > _currentMovementOriginalCostEstimate.Value + movementTimeoutTicks)
                {
                    logger.LogWarning("[Executor] Movement timeout: Took {Ticks} ticks, expected {Expected}. Cancelling.",
                        _ticksOnCurrent, _currentMovementOriginalCostEstimate.Value);
                    ClearEntityMovementInputs(entity);
                    Failed = true;
                    Finished = true;
                    return true;
                }
                
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
    /// Gets the set of blocks that need to be broken along the path.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:656-658
    /// </summary>
    public IReadOnlySet<(int X, int Y, int Z)> GetBlocksToBreak()
    {
        return _toBreak;
    }

    /// <summary>
    /// Gets the set of blocks that need to be placed along the path.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:660-662
    /// </summary>
    public IReadOnlySet<(int X, int Y, int Z)> GetBlocksToPlace()
    {
        return _toPlace;
    }

    /// <summary>
    /// Gets blocks that the player walks into during movement.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:664-666
    /// </summary>
    public IReadOnlySet<(int X, int Y, int Z)> GetBlocksToWalkInto()
    {
        return _toWalkInto;
    }

    /// <summary>
    /// Called when path position changes (movement completes).
    /// Resets tracking counters and clears inputs.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:580-583
    /// </summary>
    private void OnChangeInPathPosition(Entity entity)
    {
        ClearEntityMovementInputs(entity);
        _ticksOnCurrent = 0;
        _recalcBP = true; // Recalculate blocks on position change
    }

    /// <summary>
    /// Attempts to splice this path with a next path segment.
    /// Returns the spliced executor or this if splicing isn't possible.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:602-619
    /// </summary>
    public PathExecutor TrySplice(PathExecutor? next)
    {
        if (next == null)
        {
            return CutIfTooLong();
        }
        
        // Check if paths connect (first path's destination equals second path's start)
        if (Path.Destination.X != next.Path.Start.X || 
            Path.Destination.Y != next.Path.Start.Y || 
            Path.Destination.Z != next.Path.Start.Z)
        {
            // Paths don't connect - can't splice
            return CutIfTooLong();
        }
        
        // Find overlap point (first position in first path that appears in second path)
        var secondPosSet = new HashSet<(int X, int Y, int Z)>(next.Path.Positions);
        int firstPositionInSecond = -1;
        
        // Check up to length - 1 (overlap in last element is fine and required)
        for (int i = 0; i < Path.Length - 1; i++)
        {
            var pos = Path.Positions[i];
            if (secondPosSet.Contains(pos))
            {
                firstPositionInSecond = i;
                break;
            }
        }
        
        if (firstPositionInSecond == -1)
        {
            // No overlap found - use last position of first path
            firstPositionInSecond = Path.Length - 1;
        }
        
        // Find position in second path
        int positionInSecond = -1;
        var overlapPos = Path.Positions[firstPositionInSecond];
        for (int i = 0; i < next.Path.Positions.Count; i++)
        {
            var pos = next.Path.Positions[i];
            if (pos.X == overlapPos.X && pos.Y == overlapPos.Y && pos.Z == overlapPos.Z)
            {
                positionInSecond = i;
                break;
            }
        }
        
        if (positionInSecond == -1)
        {
            return CutIfTooLong(); // Can't find overlap
        }
        
        // Build spliced positions
        var splicedPositions = new List<(int X, int Y, int Z)>();
        splicedPositions.AddRange(Path.Positions.Take(firstPositionInSecond + 1));
        splicedPositions.AddRange(next.Path.Positions.Skip(positionInSecond + 1));
        
        // Create new path
        var splicedPath = new Path(
            splicedPositions,
            Path.Goal,
            Path.NumNodesConsidered + next.Path.NumNodesConsidered,
            next.Path.ReachesGoal
        );
        
        // Verify destination matches
        if (splicedPath.Destination.X != next.Path.Destination.X ||
            splicedPath.Destination.Y != next.Path.Destination.Y ||
            splicedPath.Destination.Z != next.Path.Destination.Z)
        {
            logger.LogWarning("[Splice] Spliced path destination doesn't match next path destination");
            return CutIfTooLong();
        }
        
        // Create new executor
        var splicedExecutor = new PathExecutor(logger, splicedPath, context);
        splicedExecutor._currentMovementIndex = _currentMovementIndex; // Preserve position
        splicedExecutor._currentMovementOriginalCostEstimate = _currentMovementOriginalCostEstimate;
        splicedExecutor._costEstimateIndex = _costEstimateIndex;
        splicedExecutor._ticksOnCurrent = _ticksOnCurrent;
        splicedExecutor.OnPlaceBlockRequest = OnPlaceBlockRequest;
        splicedExecutor.OnBreakBlockRequest = OnBreakBlockRequest;
        
        logger.LogDebug("[Splice] Spliced paths: first length {FirstLen}, second length {SecondLen}, spliced length {SplicedLen}",
            Path.Length, next.Path.Length, splicedPath.Length);
        
        return splicedExecutor;
    }

    /// <summary>
    /// Cuts the path if it's too long to prevent memory issues.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:621-641
    /// </summary>
    private PathExecutor CutIfTooLong()
    {
        const int maxPathHistoryLength = 200; // Baritone default
        const int pathHistoryCutoffAmount = 50; // Baritone default
        
        if (_currentMovementIndex > maxPathHistoryLength)
        {
            int cutoffAmt = pathHistoryCutoffAmount;
            int newStartIndex = cutoffAmt;
            int newEndIndex = Path.Length - 1;
            
            // Create new path with trimmed positions
            var trimmedPositions = Path.Positions.Skip(newStartIndex).Take(newEndIndex - newStartIndex + 1).ToList();
            var trimmedPath = new Path(
                trimmedPositions,
                Path.Goal,
                Path.NumNodesConsidered,
                Path.ReachesGoal
            );
            
            // Verify destination matches
            if (trimmedPath.Destination.X != Path.Destination.X ||
                trimmedPath.Destination.Y != Path.Destination.Y ||
                trimmedPath.Destination.Z != Path.Destination.Z)
            {
                logger.LogWarning("[CutPath] Trimmed path destination doesn't match original destination");
                return this;
            }
            
            logger.LogDebug("[CutPath] Discarding earliest segment movements, length cut from {OldLen} to {NewLen}",
                Path.Length, trimmedPath.Length);
            
            var cutExecutor = new PathExecutor(logger, trimmedPath, context);
            cutExecutor._currentMovementIndex = _currentMovementIndex - cutoffAmt;
            cutExecutor._currentMovementOriginalCostEstimate = _currentMovementOriginalCostEstimate;
            if (_costEstimateIndex.HasValue)
            {
                cutExecutor._costEstimateIndex = _costEstimateIndex.Value - cutoffAmt;
            }
            cutExecutor._ticksOnCurrent = _ticksOnCurrent;
            cutExecutor.OnPlaceBlockRequest = OnPlaceBlockRequest;
            cutExecutor.OnBreakBlockRequest = OnBreakBlockRequest;
            
            return cutExecutor;
        }
        
        return this;
    }

    /// <summary>
    /// Regardless of current path position, snap to the current player feet if possible.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:323-342
    /// </summary>
    public bool SnipsnapIfPossible(Entity entity, Level level)
    {
        var feet = GetFeetPosition(entity);
        
        // Reference: baritone lines 324-327: Don't splice if falling in air (not in water)
        if (!entity.IsOnGround && !MovementHelper.IsLiquid(level.GetBlockAt(feet.X, feet.Y, feet.Z)))
        {
            return false;
        }
        else
        {
            // Reference: baritone lines 329-333: Don't splice if strictly moving downwards (falling through water)
            if (entity.Velocity.Y < -0.1)
            {
                return false;
            }
        }
        
        // Reference: baritone line 335: Find player position in path
        int index = -1;
        for (int i = 0; i < Path.Positions.Count; i++)
        {
            var pos = Path.Positions[i];
            if (pos.X == feet.X && pos.Y == feet.Y && pos.Z == feet.Z)
            {
                index = i;
                break;
            }
        }
        
        if (index == -1)
        {
            return false;
        }
        
        // Reference: baritone line 339: Jump directly to current position
        _currentMovementIndex = index;
        ClearEntityMovementInputs(entity);
        return true;
    }

    private void ApplyMovementState(Entity entity, MovementState state, MovementBase? currentMovement)
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
            Console.WriteLine($"[Executor] Block placement requested at ({target.X}, {target.Y}, {target.Z}) by {currentMovement?.GetType()?.Name}");
            
            if (OnPlaceBlockRequest != null)
            {
                var success = OnPlaceBlockRequest(target.X, target.Y, target.Z);
                Console.WriteLine($"[Executor] Block placement result: {success}");
            }
            else
            {
                Console.WriteLine("[Executor] OnPlaceBlockRequest callback not set!");
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
            VerifyFuturePath(entity);
        }
    }

    /// <summary>
    /// Verifies that upcoming movements are still possible.
    /// If the world changes (block placed by another entity), we must abort.
    /// Reference: Baritone PathExecutor.java lines 199-204 (cancel behavior)
    /// 
    /// NOTE: We skip world-modifying movements like MovementPillar because they
    /// depend on previous movements (e.g. earlier pillars) placing blocks.
    /// Recalculating their cost against current world state would incorrectly
    /// return infinite for valid pillar chains.
    /// </summary>
    private void VerifyFuturePath(Entity entity)
    {
        // Check next 5 movements
        var verifyEnd = Math.Min(_movements.Count, _currentMovementIndex + 5);
        
        for (int i = _currentMovementIndex + 1; i < verifyEnd; i++)
        {
            var mov = _movements[i];
            
            // Skip world-modifying movements that depend on previous movements placing blocks
            // These movements are validated during initial A* calculation and during execution
            if (mov is MovementPillar)
            {
                continue; // Pillar chains place blocks, so future pillars see air until execution
            }
            
            var cost = mov.CalculateCost(context);
            
            if (cost >= ActionCosts.CostInf)
            {
                logger.LogWarning("[Executor] Path blocked at index {Index} ({Type}). Recalculated cost is Infinite. Aborting.", 
                    i, mov.GetType().Name);
                ClearEntityMovementInputs(entity); // Baritone: cancel() calls clearKeys()
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
                // Calculate cost for the movement using the context
                // This matches the cost calculated during pathfinding
                movement.CalculateCost(context);
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
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java
    /// </summary>
    private double ClosestPathDistance(Entity entity)
    {
        double best = double.MaxValue;
        var playerPos = entity.Position;
        
        foreach (var movement in _movements)
        {
            // Iterate through all valid positions for this movement (not just source/destination)
            // This handles intermediate positions for diagonal, ascend, descend movements
            foreach (var pos in movement.GetValidPositions())
            {
                var dist = DistanceToBlockCenter(playerPos, pos.X, pos.Y, pos.Z);
                if (dist < best) best = dist;
            }
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
    /// Determines if the player should sprint next tick.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:344-475
    /// </summary>
    private bool ShouldSprintNextTick(Entity entity, Level level, MovementBase currentMovement, MovementState state)
    {
        // Check if movement requested sprinting (equivalent to input override handler check)
        bool requested = state.Sprint;
        state.Sprint = false; // Reset - we'll decide here
        
        // Check if sprinting is allowed
        if (!context.CanSprint)
        {
            return false;
        }
        
        // Traverse → Ascend sprint optimization
        if (currentMovement is MovementTraverse && _currentMovementIndex < _movements.Count - 3)
        {
            var next = _movements[_currentMovementIndex + 1];
            if (next is MovementAscend ascend)
            {
                var nextNext = _currentMovementIndex + 2 < _movements.Count ? _movements[_currentMovementIndex + 2] : null;
                if (nextNext != null && IsSprintableAscend(currentMovement, ascend, nextNext, level))
                {
                    if (SkipNow(entity, currentMovement, level))
                    {
                        logger.LogDebug("[Sprint] Skipping traverse to straight ascend");
                        _currentMovementIndex++;
                        OnChangeInPathPosition(entity);
                        state.Jump = true; // Force jump for ascend
                        return true;
                    }
                    else
                    {
                        logger.LogDebug("[Sprint] Too far to the side to safely sprint ascend");
                    }
                }
            }
        }
        
        // If movement requested sprinting, allow it
        if (requested)
        {
            return true;
        }
        
        // Descend sprint logic
        if (currentMovement is MovementDescend descend)
        {
            // Frost walker and safe mode checks
            if (_currentMovementIndex < _movements.Count - 2)
            {
                var next = _movements[_currentMovementIndex + 1];
                var nextDest = next.Destination;
                var nextDestBelow = level.GetBlockAt(nextDest.X, nextDest.Y - 1, nextDest.Z);
                
                if (MovementHelper.CanUseFrostWalker(context, nextDestBelow))
                {
                    // Frost walker logic - force safe mode if needed
                    if (next is MovementTraverse || next is MovementParkour)
                    {
                        // Check if same flat direction (simplified - full implementation would use cross product)
                        var currentDirX = currentMovement.Destination.X - currentMovement.Source.X;
                        var currentDirZ = currentMovement.Destination.Z - currentMovement.Source.Z;
                        var nextDirX = next.Destination.X - next.Source.X;
                        var nextDirZ = next.Destination.Z - next.Source.Z;
                        
                        // Same flat direction check (simplified)
                        bool sameFlatDirection = (currentDirX != 0 && nextDirX != 0 && Math.Sign(currentDirX) == Math.Sign(nextDirX)) ||
                                                (currentDirZ != 0 && nextDirZ != 0 && Math.Sign(currentDirZ) == Math.Sign(nextDirZ));
                        
                        if (sameFlatDirection)
                        {
                            descend.ForceSafeMode = true;
                        }
                    }
                }
            }
            
            // Check safe mode
            if (descend.SafeMode(level) && !descend.SkipToAscend(level))
            {
                logger.LogDebug("[Sprint] Sprinting would be unsafe in descend");
                return false;
            }
            
            // Descend → Ascend skipping
            if (_currentMovementIndex < _movements.Count - 2)
            {
                var next = _movements[_currentMovementIndex + 1];
                if (next is MovementAscend nextAscend)
                {
                    // Check if directions match (descend.above().equals(next.below()))
                    var currentDirX = currentMovement.Destination.X - currentMovement.Source.X;
                    var currentDirZ = currentMovement.Destination.Z - currentMovement.Source.Z;
                    var nextDirX = nextAscend.Destination.X - nextAscend.Source.X;
                    var nextDirZ = nextAscend.Destination.Z - nextAscend.Source.Z;
                    
                    if (currentDirX == nextDirX && currentDirZ == nextDirZ)
                    {
                        logger.LogDebug("[Sprint] Skipping descend to straight ascend");
                        _currentMovementIndex++;
                        OnChangeInPathPosition(entity);
                        return true;
                    }
                }
                
                // Check canSprintFromDescendInto
                if (CanSprintFromDescendInto(level, currentMovement, next, context))
                {
                    // Check double descend case
                    if (next is MovementDescend && _currentMovementIndex < _movements.Count - 3)
                    {
                        var nextNext = _movements[_currentMovementIndex + 2];
                        if (nextNext is MovementDescend && !CanSprintFromDescendInto(level, next, nextNext, context))
                        {
                            return false;
                        }
                    }
                    
                    // Check if at destination
                    var feet = GetFeetPosition(entity);
                    if (feet.X == currentMovement.Destination.X && feet.Y == currentMovement.Destination.Y && feet.Z == currentMovement.Destination.Z)
                    {
                        _currentMovementIndex++;
                        OnChangeInPathPosition(entity);
                    }
                    
                    return true;
                }
            }
        }
        
        // MovementAscend after MovementDescend
        if (currentMovement is MovementAscend && _currentMovementIndex > 0)
        {
            var prev = _movements[_currentMovementIndex - 1];
            if (prev is MovementDescend prevDescend)
            {
                // Check if directions match
                var prevDirX = prev.Destination.X - prev.Source.X;
                var prevDirZ = prev.Destination.Z - prev.Source.Z;
                var currentDirX = currentMovement.Destination.X - currentMovement.Source.X;
                var currentDirZ = currentMovement.Destination.Z - currentMovement.Source.Z;
                
                if (prevDirX == currentDirX && prevDirZ == currentDirZ)
                {
                    // Check if player is high enough
                    var centerY = currentMovement.Source.Y + 1;
                    if (entity.Position.Y >= centerY - 0.07) // Account for soul sand and farmland
                    {
                        state.Jump = false; // Stop jumping
                        return true;
                    }
                }
            }
            
            // Traverse → Ascend sprint check
            if (_currentMovementIndex < _movements.Count - 2)
            {
                var prevMov = _currentMovementIndex > 0 ? _movements[_currentMovementIndex - 1] : null;
                var nextMov = _movements[_currentMovementIndex + 1];
                if (prevMov is MovementTraverse prevTraverse)
                {
                    if (IsSprintableAscend(prevTraverse, currentMovement, nextMov, level))
                    {
                        return true;
                    }
                }
            }
        }
        
        // MovementFall override
        if (currentMovement is MovementFall fall)
        {
            var overrideData = OverrideFall(fall, entity, level);
            if (overrideData.HasValue)
            {
                var (lookTarget, fallDest) = overrideData.Value;
                
                // Verify destination is in path
                bool destInPath = Path.Positions.Any(p => p.X == fallDest.X && p.Y == fallDest.Y && p.Z == fallDest.Z);
                if (!destInPath)
                {
                    logger.LogWarning("[FallOverride] Fall override returned illegal destination ({X}, {Y}, {Z})", 
                        fallDest.X, fallDest.Y, fallDest.Z);
                    return false;
                }
                
                // Check if at destination
                var feet = GetFeetPosition(entity);
                if (feet.X == fallDest.X && feet.Y == fallDest.Y && feet.Z == fallDest.Z)
                {
                    // Find position index in path
                    for (int idx = 0; idx < Path.Positions.Count; idx++)
                    {
                        var pos = Path.Positions[idx];
                        if (pos.X == fallDest.X && pos.Y == fallDest.Y && pos.Z == fallDest.Z)
                        {
                            _currentMovementIndex = idx;
                            OnChangeInPathPosition(entity);
                            return true;
                        }
                    }
                }
                
                // Set look target and move forward
                var (yaw, pitch) = MovementHelper.CalculateRotation(
                    entity.Position.X, entity.Position.Y + 1.6, entity.Position.Z, // Approximate head position
                    lookTarget.X, lookTarget.Y, lookTarget.Z
                );
                state.SetTarget(yaw, pitch);
                state.MoveForward = true;
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Checks if sprinting into an ascend is safe.
    /// Matches Baritone's sprintableAscend() logic from PathExecutor.java lines 531-565.
    /// </summary>
    /// <param name="current">Current movement (typically MovementTraverse)</param>
    /// <param name="next">Next movement (must be MovementAscend)</param>
    /// <param name="nextNext">Movement after ascend</param>
    /// <param name="level">World level for block checks</param>
    /// <returns>True if safe to sprint into the ascend, false otherwise</returns>
    private bool IsSprintableAscend(MovementBase current, MovementBase next, MovementBase? nextNext, Level level)
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
        
        // Baritone line 538: Check nextNext direction matches
        if (nextNext != null)
        {
            var nextNextDirX = nextNext.Destination.X - nextNext.Source.X;
            var nextNextDirZ = nextNext.Destination.Z - nextNext.Source.Z;
            if (nextNextDirX != nextDirX || nextNextDirZ != nextDirZ)
            {
                return false;
            }
        }
        
        // Baritone lines 541-546: Check floor walkability
        var currentFloor = level.GetBlockAt(current.Destination.X, current.Destination.Y - 1, current.Destination.Z);
        if (!MovementHelper.CanWalkOn(currentFloor))
        {
            return false;
        }
        
        var nextFloor = level.GetBlockAt(ascend.Destination.X, ascend.Destination.Y - 1, ascend.Destination.Z);
        if (!MovementHelper.CanWalkOn(nextFloor))
        {
            return false;
        }
        
        // Baritone line 547: Check if next movement has blocks to break
        var nextBreakBlocks = next.GetBlocksToBreak(context);
        if (nextBreakBlocks.Any())
        {
            return false; // Can't sprint if breaking blocks
        }
        
        // Baritone lines 550-559: Check 2x3 body clearance using fullyPassable
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                var checkX = current.Source.X + (x == 1 ? currentDirX : 0);
                var checkY = current.Source.Y + y;
                var checkZ = current.Source.Z + (x == 1 ? currentDirZ : 0);
                var block = level.GetBlockAt(checkX, checkY, checkZ);
                
                if (!MovementHelper.FullyPassable(block))
                {
                    return false;
                }
            }
        }
        
        // Baritone lines 561-564: Check for hazards above
        var aboveHead = level.GetBlockAt(current.Source.X, current.Source.Y + 3, current.Source.Z);
        if (aboveHead != null && MovementHelper.AvoidWalkingInto(aboveHead))
        {
            return false;
        }
        
        var aboveDestHead = level.GetBlockAt(ascend.Destination.X, ascend.Destination.Y + 2, ascend.Destination.Z);
        if (aboveDestHead != null && MovementHelper.AvoidWalkingInto(aboveDestHead))
        {
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// Checks if path execution should pause (player is backtracking on a new path being calculated).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:270-301
    /// </summary>
    private bool ShouldPause(Entity entity, Level level)
    {
        // Check if a new path is being calculated
        if (GetBestPathSoFar == null) return false;
        
        var bestPath = GetBestPathSoFar();
        if (bestPath == null) return false;
        
        // Must be on ground
        if (!entity.IsOnGround) return false;
        
        var feet = GetFeetPosition(entity);
        
        // Must be able to walk on block below
        var blockBelow = level.GetBlockAt(feet.X, feet.Y - 1, feet.Z);
        if (!MovementHelper.CanWalkOn(blockBelow)) return false;
        
        // Must not be suffocating
        var blockAtFeet = level.GetBlockAt(feet.X, feet.Y, feet.Z);
        var blockAboveFeet = level.GetBlockAt(feet.X, feet.Y + 1, feet.Z);
        if (!MovementHelper.CanWalkThrough(blockAtFeet) || !MovementHelper.CanWalkThrough(blockAboveFeet)) return false;
        
        // Current movement must be safe to cancel
        if (_currentMovementIndex >= _movements.Count) return false;
        var currentMovement = _movements[_currentMovementIndex];
        if (!currentMovement.SafeToCancel()) return false;
        
        // Best path must have at least 3 positions
        if (bestPath.Positions.Count < 3) return false;
        
        // Skip first position (it overlaps with current path)
        // Check if player position is in the new path (excluding first position)
        var positionsToCheck = bestPath.Positions.Skip(1).ToList();
        return positionsToCheck.Contains(feet);
    }

    /// <summary>
    /// Helper for sprint ascend: checks if player is centered enough to skip traverse.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:516-529
    /// </summary>
    private static bool SkipNow(Entity entity, MovementBase current, Level level)
    {
        var dirX = current.Destination.X - current.Source.X;
        var dirZ = current.Destination.Z - current.Source.Z;
        
        // Check if player is centered on the movement direction
        var offTarget = Math.Abs(dirX * (current.Source.Z + 0.5 - entity.Position.Z)) + 
                       Math.Abs(dirZ * (current.Source.X + 0.5 - entity.Position.X));
        if (offTarget > 0.1)
        {
            return false; // Not centered enough
        }
        
        // Check head clearance - if head bonk block is fully passable, we can skip
        var headBonkX = current.Source.X - dirX;
        var headBonkY = current.Source.Y + 2;
        var headBonkZ = current.Source.Z - dirZ;
        var headBonkBlock = level.GetBlockAt(headBonkX, headBonkY, headBonkZ);
        if (MovementHelper.FullyPassable(headBonkBlock))
        {
            return true;
        }
        
        // Wait a bit - check if distance to head bonk block is > 0.8
        var flatDist = Math.Abs(dirX * (headBonkX + 0.5 - entity.Position.X)) + 
                      Math.Abs(dirZ * (headBonkZ + 0.5 - entity.Position.Z));
        return flatDist > 0.8;
    }

    /// <summary>
    /// Helper for sprint logic: checks if we can sprint from a descend into the next movement.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:567-578
    /// </summary>
    private static bool CanSprintFromDescendInto(Level level, MovementBase current, MovementBase next, CalculationContext context)
    {
        if (next is MovementDescend descend)
        {
            // Check if directions match
            var currentDirX = current.Destination.X - current.Source.X;
            var currentDirZ = current.Destination.Z - current.Source.Z;
            var nextDirX = descend.Destination.X - descend.Source.X;
            var nextDirZ = descend.Destination.Z - descend.Source.Z;
            if (currentDirX == nextDirX && currentDirZ == nextDirZ)
            {
                return true; // Same direction descends
            }
        }
        
        // Check if we can walk on the block ahead of current destination
        var aheadX = current.Destination.X + (current.Destination.X - current.Source.X);
        var aheadZ = current.Destination.Z + (current.Destination.Z - current.Source.Z);
        var aheadBlock = level.GetBlockAt(aheadX, current.Destination.Y - 1, aheadZ);
        if (!MovementHelper.CanWalkOn(aheadBlock))
        {
            return false;
        }
        
        // Check if next is traverse or diagonal in same direction
        if (next is MovementTraverse traverse)
        {
            var currentDirX = current.Destination.X - current.Source.X;
            var currentDirZ = current.Destination.Z - current.Source.Z;
            var nextDirX = traverse.Destination.X - traverse.Source.X;
            var nextDirZ = traverse.Destination.Z - traverse.Source.Z;
            if (currentDirX == nextDirX && currentDirZ == nextDirZ)
            {
                return true;
            }
        }
        
        // Check if next is diagonal (with settings check)
        if (next is MovementDiagonal)
        {
            // Baritone checks allowOvershootDiagonalDescend setting
            return context.AllowDiagonalDescend;
        }
        
        return false;
    }

    /// <summary>
    /// Override fall movement to extend it with subsequent traverses for optimal landing.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java:477-514
    /// </summary>
    /// <returns>Tuple of (lookTarget, destination) if override is possible, null otherwise</returns>
    private (Models.Core.Vector3<double> LookTarget, (int X, int Y, int Z) Destination)? OverrideFall(MovementFall movement, Entity entity, Level level)
    {
        // Get direction - fall direction Y should be negative
        var dirX = movement.Destination.X - movement.Source.X;
        var dirY = movement.Destination.Y - movement.Source.Y;
        var dirZ = movement.Destination.Z - movement.Source.Z;
        
        if (dirY >= -3)
        {
            return null; // Not a significant fall
        }
        
        // Check if movement has blocks to break
        var breakBlocks = movement.GetBlocksToBreak(context);
        if (breakBlocks.Any())
        {
            return null; // It's breaking blocks, can't override
        }
        
        // Get flat direction (X, Z only)
        var flatDirX = dirX;
        var flatDirZ = dirZ;
        
        int i;
        bool foundBlockage = false;
        for (i = _currentMovementIndex + 1; i < _movements.Count - 1 && i < _currentMovementIndex + 3; i++)
        {
            var next = _movements[i];
            if (next is not MovementTraverse)
            {
                break;
            }
            
            // Check if direction matches
            var nextDirX = next.Destination.X - next.Source.X;
            var nextDirZ = next.Destination.Z - next.Source.Z;
            if (flatDirX != nextDirX || flatDirZ != nextDirZ)
            {
                break;
            }
            
            // Check clearance from next dest Y up to movement src Y + 1
            for (int y = next.Destination.Y; y <= movement.Source.Y + 1; y++)
            {
                var block = level.GetBlockAt(next.Destination.X, y, next.Destination.Z);
                if (!MovementHelper.FullyPassable(block))
                {
                    foundBlockage = true;
                    break;
                }
            }
            
            if (foundBlockage)
            {
                break;
            }
            
            // Check floor walkability
            var nextFloor = level.GetBlockAt(next.Destination.X, next.Destination.Y - 1, next.Destination.Z);
            if (!MovementHelper.CanWalkOn(nextFloor))
            {
                break;
            }
        }
        
        i--;
        if (i == _currentMovementIndex)
        {
            return null; // No valid extension exists
        }
        
        // Calculate extended destination and look target
        double len = i - _currentMovementIndex - 0.4;
        var lookTarget = new Models.Core.Vector3<double>(
            flatDirX * len + movement.Destination.X + 0.5,
            movement.Destination.Y,
            flatDirZ * len + movement.Destination.Z + 0.5
        );
        
        var extendedDest = (
            movement.Destination.X + flatDirX * (i - _currentMovementIndex),
            movement.Destination.Y,
            movement.Destination.Z + flatDirZ * (i - _currentMovementIndex)
        );
        
        return (lookTarget, extendedDest);
    }
}
