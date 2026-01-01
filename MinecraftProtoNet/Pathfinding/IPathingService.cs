using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding.Goals;
using MinecraftProtoNet.State;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;

namespace MinecraftProtoNet.Pathfinding;

/// <summary>
/// Interface for pathing service.
/// </summary>
public interface IPathingService
{
    /// <summary>
    /// Returns whether currently pathing.
    /// </summary>
    bool IsPathing { get; }

    /// <summary>
    /// Returns whether a path calculation is in progress.
    /// </summary>
    bool IsCalculating { get; }

    /// <summary>
    /// Gets the current goal.
    /// </summary>
    IGoal? Goal { get; }

    /// <summary>
    /// Sets the goal and starts pathfinding.
    /// Level is obtained automatically via IClientStateAccessor.
    /// </summary>
    /// <param name="goal">The goal to path to.</param>
    /// <param name="entity">The entity doing the pathing.</param>
    bool SetGoalAndPath(IGoal goal, Entity entity);

    /// <summary>
    /// Called each physics tick to advance pathing.
    /// This is the pre-physics callback that sets input state.
    /// </summary>
    void OnPhysicsTick(Entity entity);

    /// <summary>
    /// Cancels the current path if safe.
    /// </summary>
    bool Cancel(Entity entity);

    /// <summary>
    /// Force cancels everything.
    /// </summary>
    void ForceCancel(Entity entity);

    /// <summary>
    /// Event fired when path completes (success or failure).
    /// </summary>
    event Action<bool>? OnPathComplete;

    /// <summary>
    /// Event fired when a path is calculated.
    /// </summary>
    event Action<Path>? OnPathCalculated;

    /// <summary>
    /// Event fired when any pathing state changes (IsPathing, IsCalculating, Goal).
    /// </summary>
    event Action? OnStateChanged;
}
