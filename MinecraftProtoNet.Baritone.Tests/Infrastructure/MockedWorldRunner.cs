using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MinecraftProtoNet.Baritone.Pathfinding;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Pathfinding.Goals;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Baritone.Tests.Infrastructure;

/// <summary>
/// Orchestrates the full mocked world simulation loop.
/// Combines PathingBehavior (decision making) with TestPhysicsSimulator (movement).
/// </summary>
public class MockedWorldRunner
{
    private readonly ILogger<MockedWorldRunner> _logger;

    /// <summary>
    /// The mocked world level.
    /// </summary>
    public Level Level { get; }

    /// <summary>
    /// The test entity (player).
    /// </summary>
    public Entity Entity { get; }

    /// <summary>
    /// The pathing behavior controlling the entity.
    /// </summary>
    public PathingBehavior PathingBehavior { get; }

    /// <summary>
    /// The physics simulator.
    /// </summary>
    public TestPhysicsSimulator Physics { get; }

    /// <summary>
    /// Total ticks elapsed since runner creation.
    /// </summary>
    public int TicksElapsed { get; private set; }

    /// <summary>
    /// Maximum ticks before giving up on a goal.
    /// </summary>
    public int MaxTicksPerGoal { get; set; } = 1000;

    /// <summary>
    /// Event fired each tick for debugging/monitoring.
    /// </summary>
    public event Action<int, Entity>? OnTick;

    /// <summary>
    /// Event fired when an item is picked up.
    /// </summary>
    public event Action<TestItemEntity>? OnItemPickup;

    /// <summary>
    /// Item entity manager for item pickup simulation.
    /// </summary>
    public TestItemEntityManager ItemEntities { get; } = new();

    /// <summary>
    /// Creates a new MockedWorldRunner from a TestWorldBuilder.
    /// </summary>
    public MockedWorldRunner(TestWorldBuilder builder, ILoggerFactory? loggerFactory = null)
    {
        loggerFactory ??= NullLoggerFactory.Instance;
        _logger = loggerFactory.CreateLogger<MockedWorldRunner>();

        var (level, entity) = builder.BuildWithPlayer();
        Level = level;
        Entity = entity;
        Physics = new TestPhysicsSimulator();
        PathingBehavior = new PathingBehavior(
            loggerFactory.CreateLogger<PathingBehavior>(),
            loggerFactory,
            level);

        // Use the real A* pathfinder
        PathingBehavior.PathFinderFactory = (ctx, goal, x, y, z) => new AStarPathFinder(ctx, goal, x, y, z);

        // Initialize entity ground state by simulating a few physics ticks
        Physics.TickUntilGrounded(Entity, Level, 20);
    }

    /// <summary>
    /// Runs a single tick of the simulation.
    /// </summary>
    public void Tick()
    {
        // 1. Pathing decision (sets entity input)
        PathingBehavior.OnTick(Entity);

        // 2. Physics simulation (moves entity)
        Physics.Tick(Entity, Level);

        // 3. Item pickup check
        var pickedUp = ItemEntities.TickAndCheckPickup(Entity.Position);
        foreach (var item in pickedUp)
        {
            // Add to player inventory
            AddItemToInventory(item.Item);
            OnItemPickup?.Invoke(item);
        }

        TicksElapsed++;
        OnTick?.Invoke(TicksElapsed, Entity);
    }

    /// <summary>
    /// Runs the simulation until the goal is reached or max ticks exceeded.
    /// </summary>
    /// <param name="goal">The goal to reach.</param>
    /// <param name="maxTicks">Maximum ticks to attempt (default: MaxTicksPerGoal).</param>
    /// <returns>Result indicating success, ticks used, and final position.</returns>
    public RunResult RunToGoal(IGoal goal, int maxTicks = -1)
    {
        if (maxTicks < 0) maxTicks = MaxTicksPerGoal;

        // Start pathfinding
        var started = PathingBehavior.SetGoalAndPath(goal, Entity);
        if (!started)
        {
            // Already at goal?
            var feet = GetFeetPosition();
            if (goal.IsInGoal(feet.X, feet.Y, feet.Z))
            {
                return new RunResult(true, 0, Entity.Position, "Already at goal");
            }
            return new RunResult(false, 0, Entity.Position, "Failed to start pathfinding");
        }

        // Wait for initial path calculation to complete and path to be available
        // The path calculation runs in a background task - we need to wait for:
        // 1. Calculation to complete (IsCalculating becomes false)
        // 2. Path executor to be set (IsPathing becomes true)
        // There's a brief window between _current being set and _inProgress being cleared
        // so we wait for IsPathing to stabilize
        int waitMs = 0;
        const int maxWaitMs = 2000; // 2 second max wait
        const int pollIntervalMs = 10;
        
        while (waitMs < maxWaitMs)
        {
            // If we have a path, we're good
            if (PathingBehavior.IsPathing)
            {
                break;
            }
            
            // If still calculating, keep waiting
            if (PathingBehavior.IsCalculating)
            {
                Thread.Sleep(pollIntervalMs);
                waitMs += pollIntervalMs;
                continue;
            }
            
            // Calculation finished but no path yet - give a few more ms for thread scheduling
            Thread.Sleep(pollIntervalMs);
            waitMs += pollIntervalMs;
            
            // Check again
            if (PathingBehavior.IsPathing)
            {
                break;
            }
            
            // Calculation done but still no path - path calculation likely failed
            // Give a small additional grace period then check one more time
            if (waitMs >= 100 && !PathingBehavior.IsCalculating && !PathingBehavior.IsPathing)
            {
                // One more try: run a tick which might trigger recalculation
                PathingBehavior.OnTick(Entity);
                Thread.Sleep(50);
                
                if (PathingBehavior.IsPathing)
                {
                    break;
                }
                
                // No path found
                return new RunResult(false, 0, Entity.Position, "No path found");
            }
        }

        if (!PathingBehavior.IsPathing)
        {
            return new RunResult(false, 0, Entity.Position, "Path calculation timed out");
        }

        // Run simulation
        int startTick = TicksElapsed;
        for (int i = 0; i < maxTicks; i++)
        {
            Tick();

            var feet = GetFeetPosition();
            if (goal.IsInGoal(feet.X, feet.Y, feet.Z))
            {
                return new RunResult(true, TicksElapsed - startTick, Entity.Position, "Goal reached");
            }

            // Check for pathing failure
            if (!PathingBehavior.IsPathing && !PathingBehavior.IsCalculating)
            {
                // Path finished but not at goal - might be recalculating
                Thread.Sleep(50);
                if (!PathingBehavior.IsPathing && !PathingBehavior.IsCalculating)
                {
                    return new RunResult(false, TicksElapsed - startTick, Entity.Position, "Pathing stopped");
                }
            }
        }

        return new RunResult(false, maxTicks, Entity.Position, "Max ticks exceeded");
    }

    /// <summary>
    /// Gets the entity's feet position as block coordinates.
    /// </summary>
    public (int X, int Y, int Z) GetFeetPosition()
    {
        return (
            (int)Math.Floor(Entity.Position.X),
            (int)Math.Floor(Entity.Position.Y),
            (int)Math.Floor(Entity.Position.Z)
        );
    }

    /// <summary>
    /// Teleports the entity to a new position instantly.
    /// </summary>
    public void TeleportEntity(double x, double y, double z)
    {
        Entity.Position = new Vector3<double>(x, y, z);
        Entity.Velocity = Vector3<double>.Zero;
        Entity.IsOnGround = false;
        Physics.TickUntilGrounded(Entity, Level);
    }

    /// <summary>
    /// Adds an item to the player's inventory in the first available slot.
    /// </summary>
    private void AddItemToInventory(Slot item)
    {
        // Find first empty slot in inventory (9-35 for main inventory)
        for (short slot = 9; slot <= 44; slot++)
        {
            var existing = Entity.Inventory.GetSlot(slot);
            if (existing.ItemId == null || existing.ItemId <= 0 || existing.ItemCount <= 0)
            {
                Entity.Inventory.SetSlot(slot, item);
                return;
            }
        }
    }
}

/// <summary>
/// Result of a goal-seeking run.
/// </summary>
public record RunResult(bool Success, int TicksUsed, Vector3<double> FinalPosition, string Message);
