using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding.Goals;
using MinecraftProtoNet.State;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;

namespace MinecraftProtoNet.Baritone.Pathfinding;

/// <summary>
/// High-level path management with async calculation.
/// Based on Baritone's PathingBehavior.java.
/// </summary>
public class PathingBehavior(ILogger<PathingBehavior> logger, ILoggerFactory loggerFactory, Level level)
{
    private PathExecutor? _current;
    private PathExecutor? _next;
    private IGoal? _goal;
    private CalculationContext? _context;

    private IPathFinder? _inProgress;
    private readonly Lock _pathCalcLock = new();
    private readonly Lock _pathPlanLock = new();

    private bool _safeToCancel = true;
    private int _ticksElapsedSoFar;
    private (int X, int Y, int Z)? _startPosition;
    
    // Backoff and State
    private int _failureCount;
    private int _ticksUntilNextCalculation;
    private bool _paused;

    // Track current entity for teleport event subscription
    private Entity? _subscribedEntity;

    /// <summary>
    /// Default timeout for primary path calculation (ms).
    /// </summary>
    public long PrimaryTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// Extended timeout when no path found yet (ms).
    /// </summary>
    public long FailureTimeoutMs { get; set; } = 4000;

    /// <summary>
    /// Ticks remaining before starting to plan ahead.
    /// </summary>
    public int PlanningTickLookahead { get; set; } = 150;

    /// <summary>
    /// Callback for placing blocks. Takes (targetX, targetY, targetZ) and returns success.
    /// Set this before calling SetGoalAndPath to enable block placement for pillaring.
    /// </summary>
    public Func<int, int, int, bool>? OnPlaceBlockRequest { get; set; }

    /// <summary>
    /// Callback for breaking blocks. Takes (targetX, targetY, targetZ) and returns success_ack.
    /// </summary>
    public Func<int, int, int, bool>? OnBreakBlockRequest { get; set; }

    /// <summary>
    /// Factory for creating pathfinders. Can be replaced for testing.
    /// </summary>
    public Func<CalculationContext, IGoal, int, int, int, IPathFinder> PathFinderFactory { get; set; } = 
        (ctx, goal, x, y, z) => new AStarPathFinder(ctx, goal, x, y, z);

    /// <summary>
    /// Callback to get best tool speed.
    /// </summary>
    public Func<Models.World.Chunk.BlockState, float>? GetBestToolSpeed { get; set; }

    /// <summary>
    /// Gets the current goal.
    /// </summary>
    public IGoal? Goal => _goal;

    /// <summary>
    /// Returns whether currently pathing.
    /// </summary>
    public bool IsPathing => _current != null && !_current.Finished;

    /// <summary>
    /// Returns whether a path calculation is in progress.
    /// </summary>
    public bool IsCalculating => _inProgress != null;

    /// <summary>
    /// Gets the current path executor.
    /// </summary>
    public PathExecutor? Current => _current;

    /// <summary>
    /// Gets whether it's safe to cancel the current path.
    /// </summary>
    public bool IsSafeToCancel => _safeToCancel;

    /// <summary>
    /// Gets whether pathing is currently paused.
    /// </summary>
    public bool IsPaused => _paused;

    /// <summary>
    /// Pauses pathing execution.
    /// </summary>
    public void Pause()
    {
        _paused = true;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Resumes pathing execution.
    /// </summary>
    public void Resume()
    {
        _paused = false;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Sets the goal and starts pathfinding.
    /// </summary>
    public bool SetGoalAndPath(IGoal goal, Entity entity)
    {
        _goal = goal;
        _context = new CalculationContext(level);
        
        // Update context based on current entity state
        _context.CanSprint = true; // Could check hunger
        
        // Check inventory for building blocks
        // For now, we'll assume if we have ANY items in specific slots or just hack check
        // Ideally we check internal inventory state. 
        // Baritone checks Inventory.hasThrowaway()
        bool hasThrowaway = false;
        if (entity.Inventory != null)
        {
             hasThrowaway = entity.Inventory.HasThrowawayBlocks();
        }

        _context.HasThrowaway = hasThrowaway; 
        
        // Pass tool speed callback
        _context.GetBestToolSpeed = GetBestToolSpeed;

        var startPos = GetPathStart(entity);
        if (goal.IsInGoal(startPos.X, startPos.Y, startPos.Z))
        {
            return false; // Already at goal
        }

        lock (_pathPlanLock)
        {
            if (_current != null) return false;

            lock (_pathCalcLock)
            {
                if (_inProgress != null) return false;

                _startPosition = startPos;
                _ticksElapsedSoFar = 0;
                StartPathCalculation(startPos);
                OnStateChanged?.Invoke();
                return true;
            }
        }
    }

    /// <summary>
    /// Called each tick to advance pathing.
    /// </summary>
    public void OnTick(Entity entity)
    {
        if (_paused) return;

        if (_ticksUntilNextCalculation > 0)
        {
            _ticksUntilNextCalculation--;
        }

        lock (_pathPlanLock)
        {
            if (_current == null)
            {
                // Unsubscribe from teleport events when no active path
                if (_subscribedEntity != null)
                {
                    _subscribedEntity.OnServerTeleport -= HandleServerTeleport;
                    _subscribedEntity = null;
                }

                // Retry if we have a goal but no calculation or path
                lock (_pathCalcLock)
                {
                    if (_inProgress == null && _goal != null && _ticksUntilNextCalculation == 0)
                    {
                        var startPos = GetPathStart(entity);
                        StartPathCalculation(startPos);
                    }
                }
                return;
            }
            
            // Subscribe to teleport events if not already subscribed
            if (_subscribedEntity != entity)
            {
                if (_subscribedEntity != null)
                {
                    _subscribedEntity.OnServerTeleport -= HandleServerTeleport;
                }
                entity.OnServerTeleport += HandleServerTeleport;
                _subscribedEntity = entity;
            }

            _safeToCancel = _current.OnTick(entity, level);
            _ticksElapsedSoFar++;

            if (_current.Failed || _current.Finished)
            {
                if (_goal != null)
                {
                    var feet = GetFeetPosition(entity);
                    if (_goal.IsInGoal(feet.X, feet.Y, feet.Z))
                    {
                        // Reached goal!
                        _current = null;
                        _next = null;
                        entity.ClearMovementInput();
                        OnPathComplete?.Invoke(true);
                        OnStateChanged?.Invoke();
                        return;
                    }
                }

                if (_current.Failed)
                {
                    OnPathComplete?.Invoke(false);
                    
                    // If failed due to teleport loop, cancel entirely - don't auto-retry
                    if (_current.FailedDueToTeleportLoop)
                    {
                        logger.LogWarning("[PathingBehavior] Path failed due to teleport loop - cancelling goal to prevent infinite retry");
                        _current = null;
                        _next = null;
                        _goal = null;
                        entity.ClearMovementInput();
                        OnStateChanged?.Invoke();
                        return;
                    }
                }

                // Try to continue with next segment
                if (_next != null)
                {
                    _current = _next;
                    _next = null;
                    _current.OnTick(entity, level);
                    OnStateChanged?.Invoke();
                    return;
                }

                // Need to recalculate
                lock (_pathCalcLock)
                {
                    if (_inProgress == null && _goal != null)
                    {
                        var startPos = GetPathStart(entity);
                        StartPathCalculation(startPos);
                    }
                    else
                    {
                        // No next path and no calculation in progress, stop.
                        entity.ClearMovementInput();
                    }
                }

                _current = null;
                OnStateChanged?.Invoke();
                return;
            }

            // Plan ahead if needed
            lock (_pathCalcLock)
            {
                if (_inProgress != null || _next != null || _goal == null)
                {
                    return;
                }

                // Check if current path reaches goal
                if (_goal.IsInGoal(_current.Path.Destination.X, _current.Path.Destination.Y, _current.Path.Destination.Z))
                {
                    return; // Will reach goal, no need to plan ahead
                }

                // Start planning next segment
                StartPathCalculation(_current.Path.Destination);
            }
        }
    }
    
    /// <summary>
    /// Handles server teleport events by forwarding to current executor.
    /// </summary>
    private void HandleServerTeleport(Models.Core.Vector3<double> position)
    {
        lock (_pathPlanLock)
        {
            _current?.OnServerTeleport(position);
        }
    }

    /// <summary>
    /// Cancels the current path if safe.
    /// </summary>
    public bool Cancel(Entity entity)
    {
        if (!_safeToCancel) return false;

        lock (_pathCalcLock)
        {
            _inProgress?.Cancel();
            _inProgress = null;
        }

        lock (_pathPlanLock)
        {
            _current = null;
            _next = null;
            _goal = null;
            entity.ClearMovementInput();
            
            // Unsubscribe from teleport events
            if (_subscribedEntity != null)
            {
                _subscribedEntity.OnServerTeleport -= HandleServerTeleport;
                _subscribedEntity = null;
            }
        }

        OnStateChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Force cancels everything.
    /// </summary>
    public void ForceCancel(Entity entity)
    {
        lock (_pathCalcLock)
        {
            _inProgress?.Cancel();
            _inProgress = null;
        }

        lock (_pathPlanLock)
        {
            _current = null;
            _next = null;
            _goal = null;
            entity.ClearMovementInput();
            
            // Unsubscribe from teleport events
            if (_subscribedEntity != null)
            {
                _subscribedEntity.OnServerTeleport -= HandleServerTeleport;
                _subscribedEntity = null;
            }
        }
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Event fired when path completes (success or failure).
    /// </summary>
    public event Action<bool>? OnPathComplete;

    /// <summary>
    /// Event fired when a path is calculated.
    /// </summary>
    public event Action<Path>? OnPathCalculated;

    /// <summary>
    /// Event fired when any pathing state changes (IsPathing, IsCalculating, Goal).
    /// </summary>
    public event Action? OnStateChanged;

    private void StartPathCalculation((int X, int Y, int Z) start)
    {
        if (_goal == null || _context == null) return;

        var pathfinder = PathFinderFactory(_context, _goal, start.X, start.Y, start.Z);
        _inProgress = pathfinder;
        OnStateChanged?.Invoke();

        // Run async
        Task.Run(() =>
        {
            PathCalculationResultType resultType;
            Path? path;
            try
            {
                (resultType, path) = pathfinder.Calculate(PrimaryTimeoutMs, FailureTimeoutMs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[PathingBehavior] Exception during path calculation");
                resultType = PathCalculationResultType.Failure;
                path = null;
            }

            lock (_pathPlanLock)
            {
                if (resultType == PathCalculationResultType.Success || resultType == PathCalculationResultType.PartialSuccess)
                {
                    if (path != null)
                    {
                        _failureCount = 0; // Reset backoff on success
                        OnPathCalculated?.Invoke(path);

                        // Detect stuck state: if path has only 1 position, there are no valid moves
                        if (path.Positions.Count <= 1)
                        {
                            logger.LogWarning("[PathingBehavior] Path has no movements (1 position only). Bot is stuck - no valid moves from current location.");
                            lock (_pathCalcLock)
                            {
                                _inProgress = null;
                            }
                            OnStateChanged?.Invoke();
                            return;
                        }

                        var executor = new PathExecutor(loggerFactory.CreateLogger<PathExecutor>(), path, _context);
                        executor.OnPlaceBlockRequest = OnPlaceBlockRequest;
                        executor.OnBreakBlockRequest = OnBreakBlockRequest;

                        if (_current == null)
                        {
                            _current = executor;
                        }
                        else if (_next == null)
                        {
                            _next = executor;
                        }
                    }
                }
                else if (resultType == PathCalculationResultType.Failure || resultType == PathCalculationResultType.Timeout)
                {
                    _failureCount++;
                    // Backoff: 20 ticks (1s), 40, 80, up to 10s (200 ticks)
                    _ticksUntilNextCalculation = Math.Min(200, 20 * (int)Math.Pow(2, Math.Min(5, _failureCount - 1)));
                    logger.LogWarning("[PathingBehavior] Path calculation {Result}. Failure count: {Count}. Backoff: {Ticks} ticks", 
                        resultType, _failureCount, _ticksUntilNextCalculation);
                }

                lock (_pathCalcLock)
                {
                    _inProgress = null;
                }
                OnStateChanged?.Invoke();
            }
        });
    }

    private (int X, int Y, int Z) GetPathStart(Entity entity)
    {
        var feet = GetFeetPosition(entity);

        // Check if we can walk on the block below
        var floor = level.GetBlockAt(feet.X, feet.Y - 1, feet.Z);
        if (floor != null && floor.BlocksMotion)
        {
            return feet;
        }

        // If on ground but floor doesn't block motion, we might be on an edge
        if (entity.IsOnGround)
        {
            // Check adjacent blocks for valid standing position
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    var checkFloor = level.GetBlockAt(feet.X + dx, feet.Y - 1, feet.Z + dz);
                    if (checkFloor != null && checkFloor.BlocksMotion)
                    {
                        return (feet.X + dx, feet.Y, feet.Z + dz);
                    }
                }
            }
        }

        return feet;
    }

    private static (int X, int Y, int Z) GetFeetPosition(Entity entity)
    {
        return ((int)Math.Floor(entity.Position.X),
                (int)Math.Floor(entity.Position.Y),
                (int)Math.Floor(entity.Position.Z));
    }

    /// <summary>
    /// Estimates ticks remaining to reach goal.
    /// </summary>
    public double? EstimatedTicksToGoal(Entity entity)
    {
        if (_goal == null || _startPosition == null || _ticksElapsedSoFar == 0)
        {
            return null;
        }

        var currentPos = GetFeetPosition(entity);
        if (_goal.IsInGoal(currentPos.X, currentPos.Y, currentPos.Z))
        {
            return 0;
        }

        var currentHeuristic = _goal.Heuristic(currentPos.X, currentPos.Y, currentPos.Z);
        var startHeuristic = _goal.Heuristic(_startPosition.Value.X, _startPosition.Value.Y, _startPosition.Value.Z);

        if (Math.Abs(currentHeuristic - startHeuristic) < 0.01)
        {
            return null; // No progress
        }

        // Estimate based on progress so far
        var progress = (startHeuristic - currentHeuristic) / startHeuristic;
        if (progress <= 0) return null;

        return _ticksElapsedSoFar / progress - _ticksElapsedSoFar;
    }
}
