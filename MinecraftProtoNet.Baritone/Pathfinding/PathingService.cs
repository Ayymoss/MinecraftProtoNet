using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Pathfinding.Goals;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Core.Abstractions;

namespace MinecraftProtoNet.Baritone.Pathfinding;

/// <summary>
/// Service for managing pathfinding and movement.
/// Integrates with PhysicsService via pre-physics callback.
/// </summary>
public class PathingService : IPathingService, IDisposable
{
    private readonly ILogger<PathingService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IClientStateAccessor _stateAccessor;
    private readonly IInteractionManager _interactionManager;
    private readonly IInventoryManager _inventoryManager;
    private PathingBehavior? _behavior;
    private Level? _cachedLevel;
    
    private readonly IGameLoop _gameLoop;
    
    // Mining state tracking
    private (int X, int Y, int Z)? _currentMiningTarget;
    private long _lastMiningRequestTick;

    public PathingService(ILogger<PathingService> logger,
        ILoggerFactory loggerFactory,
        IClientStateAccessor stateAccessor, 
        IInteractionManager interactionManager,
        IInventoryManager inventoryManager,
        IGameLoop gameLoop)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _stateAccessor = stateAccessor;
        _interactionManager = interactionManager;
        _inventoryManager = inventoryManager;
        _gameLoop = gameLoop;
        
        _gameLoop.PhysicsTick += OnPhysicsTick;
        
        // Initialize default place block handler
        OnPlaceBlockRequest = (x, y, z) =>
        {
            // Simple throwaway block logic
            // TODO: Move this to a proper InventoryBehavior/Selector
            // For now, try to find common blocks
            var validBlocks = new[] { "minecraft:cobblestone", "minecraft:dirt", "minecraft:stone", "minecraft:netherrack" };
            
            // We need to find one of these in the inventory and equip it
            // Since this lambda is sync, we can't await easily. 
            // However, PathingBehavior.OnTick is sync... but interaction manager is async.
            // We'll fire and forget for now, as the movement loop handles retries.
            _ = Task.Run(async () => 
            {
                if (await EquipThrowawayBlock(validBlocks))
                {
                    await _interactionManager.PlaceBlockAsync();
                }
            });
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
                // Wire up breaking with tool switching and state tracking
                OnBreakBlockRequest = (x, y, z) => 
                {
                    // Check if we already requested this block recently
                    long currentTick = currentLevel.WorldAge; 
                    // Note: Level.GameTime might be world time. We prefer a monotonic tick counter.
                    // Assuming GameTime increases by 1 per tick.

                    if (_currentMiningTarget.HasValue && 
                        _currentMiningTarget.Value.X == x && 
                        _currentMiningTarget.Value.Y == y && 
                        _currentMiningTarget.Value.Z == z)
                    {
                        // Retry if stuck for too long (e.g. 20 ticks = 1 second)
                        if (currentTick - _lastMiningRequestTick > 20)
                        {
                            // Stalled? Re-issue mining command.
                            // Fall through to re-execution logic, but update tick.
                        }
                        else
                        {
                            // Already mining this block and within timeout, wait for server to update
                            return true;
                        }
                    }

                    _currentMiningTarget = (x, y, z);
                    _lastMiningRequestTick = currentTick;

                    _ = Task.Run(async () =>
                    {
                        try 
                        {
                            var block = currentLevel.GetBlockAt(x, y, z);
                            if (block != null && !block.IsAir)
                            {
                                await _inventoryManager.EquipBestTool(block);
                                await _interactionManager.DigBlockAsync();
                            }
                        }
                        finally
                        {
                            // We don't clear _currentMiningTarget here immediately 
                            // because we want to wait for the block to actually disappear (handled by bot tick/world update)
                            // However, if the bot moves away or gives up, we might get stuck.
                            // But PathExecutor recalculates 'toBreak'. If this block is no longer needed, 
                            // OnBreakBlockRequest won't be called for it.
                            
                            // ISSUE: If we successfully sent the packet, we just wait.
                            // If the block vanishes, PathExecutor stops calling us.
                            // So we need a way to reset if the target changes. 
                            // The 'if' check at the top handles 'same block'.
                            // If 'x,y,z' changes, we proceed to mine the new one.
                        }
                    });
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

    /// <summary>
    /// Starts following the specified entity.
    /// Runs a background loop to update the path as the entity moves.
    /// </summary>
    public void StartFollowing(Entity target)
    {
        EnsureInitialized();
        if (_behavior == null) return;
        
        // Cancel any existing path/follow
        _behavior.ForceCancel(_stateAccessor.LocalPlayer!);
        
        _ = Task.Run(async () => await FollowLoop(target));
    }

    private async Task FollowLoop(Entity target)
    {
        var myself = _stateAccessor.LocalPlayer;
        if (myself == null || _behavior == null) return;

        _logger.LogInformation("Started following entity {EntityId} at {Position}", target.EntityId, target.Position);

        try
        {
            while (true)
            {
                // Stop if cancelled or services disposed
                // (Note: Proper CancellationToken support would be better)
                if (_behavior.IsPathing == false && _behavior.Goal != null && !(_behavior.Goal is GoalNear))
                {
                    // If we have a goal that ISN'T GoalNear (e.g. user ran /goto), stop following
                     _logger.LogInformation("Stopping follow because a different goal was set.");
                    break;
                }

                var targetPos = target.Position;
                var currentGoal = _behavior.Goal as GoalNear;

                // Check if we need to repath
                // 1. No goal set yet
                // 2. Target moved significantly (> 3 blocks) from current goal center
                bool shouldRepath = false;
                if (currentGoal == null)
                {
                    shouldRepath = true;
                }
                else
                {
                    var distSq = (targetPos.X - currentGoal.X) * (targetPos.X - currentGoal.X) +
                                 (targetPos.Y - currentGoal.Y) * (targetPos.Y - currentGoal.Y) +
                                 (targetPos.Z - currentGoal.Z) * (targetPos.Z - currentGoal.Z);
                    if (distSq > 9) // 3 blocks squared
                    {
                        shouldRepath = true;
                    }
                }

                if (shouldRepath)
                {
                    var newGoal = new GoalNear((int)targetPos.X, (int)targetPos.Y, (int)targetPos.Z, 3); // 3 block range
                    
                    // Only log if major change or first time
                    // _logger.LogInformation("Repathing to target at {Pos}", targetPos);
                    
                    // SetGoalAndPath will cancel current path if running
                    // SetGoalAndPath will cancel current path if running
                     _behavior.SetGoalAndPath(newGoal, myself);
                }
                
                await Task.Delay(250); // Check 4 times a second
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FollowLoop");
        }
    }

    /// <summary>
    /// Attempts to equip a throwaway block from the given list.
    /// </summary>
    private async Task<bool> EquipThrowawayBlock(string[] validBlockNames)
    {
        return await _inventoryManager.EquipItemMatches(validBlockNames);
    }

    public void Dispose()
    {
        _gameLoop.PhysicsTick -= OnPhysicsTick;
        GC.SuppressFinalize(this);
    }
}
