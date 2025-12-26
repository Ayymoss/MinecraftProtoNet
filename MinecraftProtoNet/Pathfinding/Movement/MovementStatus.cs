namespace MinecraftProtoNet.Pathfinding.Movement;

/// <summary>
/// Status of a movement during execution.
/// Based on Baritone's MovementStatus.
/// </summary>
public enum MovementStatus
{
    /// <summary>
    /// Movement is preparing (e.g., breaking blocks in the way).
    /// </summary>
    Prepping,

    /// <summary>
    /// Movement is ready to start but waiting for optimal conditions.
    /// </summary>
    Waiting,

    /// <summary>
    /// Movement is actively being executed.
    /// </summary>
    Running,

    /// <summary>
    /// Movement completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Movement failed and cannot be completed.
    /// </summary>
    Failed,

    /// <summary>
    /// Movement target is unreachable.
    /// </summary>
    Unreachable,

    /// <summary>
    /// Movement was cancelled.
    /// </summary>
    Cancelled
}
