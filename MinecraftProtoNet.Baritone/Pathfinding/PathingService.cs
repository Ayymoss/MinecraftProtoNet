using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Pathfinding.Goals;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;
using MinecraftProtoNet.Core.Abstractions;

namespace MinecraftProtoNet.Baritone.Pathfinding;

/// <summary>
/// Service for managing pathfinding and movement.
/// Integrates with PhysicsService via pre-physics callback.
/// </summary>
public class PathingService : IPathingService
{
    private readonly ILogger<PathingService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IClientStateAccessor _stateAccessor;
    private readonly BlockInteractionService _interactionService;
    private readonly IInventoryManager _inventoryManager;
    private PathingBehavior? _behavior;
    private Level? _cachedLevel;

    public PathingService(ILogger<PathingService> logger,
        ILoggerFactory loggerFactory,
        IClientStateAccessor stateAccessor, 
        BlockInteractionService interactionService,
        IInventoryManager inventoryManager)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _stateAccessor = stateAccessor;
        _interactionService = interactionService;
        _inventoryManager = inventoryManager;
        
        // Initialize default place block handler
        OnPlaceBlockRequest = (x, y, z) =>
        {
            _ = _interactionService.PlaceBlockAt(x, y, z);
            return true;
        };
    }

    /// <summary>
    /// Ensures PathingBehavior is initialized with the current level.
    /// Re-initializes if level has changed.
    /// </summary>
    private void EnsureInitialized()
    {
        var currentLevel = _stateAccessor.Level;
        if (_cachedLevel != currentLevel || _behavior == null)
        {
            _cachedLevel = currentLevel;
            _behavior = new PathingBehavior(_loggerFactory.CreateLogger<PathingBehavior>(), _loggerFactory, currentLevel)
            {
                OnPlaceBlockRequest = OnPlaceBlockRequest,
                // Wire up breaking - use fire-and-forget as PathExecutor manages retry loop
                OnBreakBlockRequest = (x, y, z) => 
                {
                    _ = _interactionService.BreakBlockAt(x, y, z);
                    return true;
                },
                // Wire up tool speed lookup
                GetBestToolSpeed = block => _inventoryManager.GetBestDigSpeed(block)
            };

            // Forward events
            _behavior.OnPathCalculated += path => OnPathCalculated?.Invoke(path);
            _behavior.OnPathComplete += success => OnPathComplete?.Invoke(success);
            _behavior.OnStateChanged += () => OnStateChanged?.Invoke();
        }
    }

    /// <inheritdoc/>
    public bool IsPathing => _behavior?.IsPathing ?? false;

    /// <inheritdoc/>
    public bool IsCalculating => _behavior?.IsCalculating ?? false;

    /// <inheritdoc/>
    public IGoal? Goal => _behavior?.Goal;

    /// <inheritdoc/>
    public bool SetGoalAndPath(IGoal goal, Entity entity)
    {
        EnsureInitialized();
        return _behavior!.SetGoalAndPath(goal, entity);
    }

    /// <inheritdoc/>
    public void OnPhysicsTick(Entity entity)
    {
        if (_behavior == null) return;
        _behavior.OnTick(entity);
    }

    /// <inheritdoc/>
    public bool Cancel(Entity entity) => _behavior?.Cancel(entity) ?? false;

    /// <inheritdoc/>
    public void ForceCancel(Entity entity) => _behavior?.ForceCancel(entity);

    /// <inheritdoc/>
    public event Action<bool>? OnPathComplete;

    /// <inheritdoc/>
    public event Action<Path>? OnPathCalculated;

    /// <inheritdoc/>
    public event Action? OnStateChanged;

    /// <summary>
    /// Gets the PathingBehavior for advanced usage. May be null if not initialized.
    /// </summary>
    public PathingBehavior? Behavior => _behavior;

    /// <summary>
    /// Callback for placing blocks during pathfinding (e.g. pillaring).
    /// Takes (targetX, targetY, targetZ) and returns success.
    /// </summary>
    public Func<int, int, int, bool>? OnPlaceBlockRequest { get; set; }
}
