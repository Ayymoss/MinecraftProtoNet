using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Pathfinding.Goals;

namespace MinecraftProtoNet.Baritone.Tests.Infrastructure;

/// <summary>
/// Runs a sequence of goals (checkpoints) and validates each.
/// Useful for testing complex navigation scenarios with multiple waypoints.
/// </summary>
public class CheckpointRunner
{
    private readonly MockedWorldRunner _runner;

    /// <summary>
    /// Maximum ticks allowed per checkpoint.
    /// </summary>
    public int MaxTicksPerCheckpoint { get; set; } = 500;

    /// <summary>
    /// Whether to cancel pathfinding between checkpoints.
    /// </summary>
    public bool CancelBetweenCheckpoints { get; set; } = true;

    /// <summary>
    /// Event fired when a checkpoint is reached.
    /// </summary>
    public event Action<int, int>? OnCheckpointReached;

    /// <summary>
    /// Event fired when a checkpoint is failed.
    /// </summary>
    public event Action<int, string>? OnCheckpointFailed;

    public CheckpointRunner(MockedWorldRunner runner)
    {
        _runner = runner;
    }

    /// <summary>
    /// Runs through all checkpoints sequentially.
    /// </summary>
    /// <param name="checkpoints">List of goals to reach in order.</param>
    /// <param name="maxTicksPerCheckpoint">Optional override for per-checkpoint tick limit.</param>
    /// <returns>Result indicating overall success and per-checkpoint stats.</returns>
    public CheckpointResult RunCheckpoints(List<IGoal> checkpoints, int maxTicksPerCheckpoint = -1)
    {
        if (maxTicksPerCheckpoint < 0) maxTicksPerCheckpoint = MaxTicksPerCheckpoint;

        var ticksPerCheckpoint = new List<int>();
        var failedAt = -1;
        var failReason = "";
        var totalTicks = 0;

        for (int i = 0; i < checkpoints.Count; i++)
        {
            var goal = checkpoints[i];

            if (CancelBetweenCheckpoints && i > 0)
            {
                _runner.PathingBehavior.ForceCancel(_runner.Entity);
            }

            var result = _runner.RunToGoal(goal, maxTicksPerCheckpoint);
            ticksPerCheckpoint.Add(result.TicksUsed);
            totalTicks += result.TicksUsed;

            if (result.Success)
            {
                OnCheckpointReached?.Invoke(i, result.TicksUsed);
            }
            else
            {
                failedAt = i;
                failReason = result.Message;
                OnCheckpointFailed?.Invoke(i, result.Message);
                break;
            }
        }

        return new CheckpointResult(
            AllReached: failedAt == -1,
            TotalTicks: totalTicks,
            TicksPerCheckpoint: ticksPerCheckpoint,
            FailedAtCheckpoint: failedAt,
            FailReason: failReason
        );
    }

    /// <summary>
    /// Creates a simple checkpoint from block coordinates.
    /// </summary>
    public static IGoal CreateCheckpoint(int x, int y, int z) => new GoalBlock(x, y, z);

    /// <summary>
    /// Creates checkpoints from a list of coordinate tuples.
    /// </summary>
    public static List<IGoal> CreateCheckpoints(params (int X, int Y, int Z)[] coords)
    {
        return coords.Select(c => (IGoal)new GoalBlock(c.X, c.Y, c.Z)).ToList();
    }
}

/// <summary>
/// Result of running a checkpoint sequence.
/// </summary>
public record CheckpointResult(
    bool AllReached,
    int TotalTicks,
    List<int> TicksPerCheckpoint,
    int FailedAtCheckpoint,
    string FailReason);
