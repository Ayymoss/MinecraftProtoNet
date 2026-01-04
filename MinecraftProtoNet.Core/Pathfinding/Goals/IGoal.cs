namespace MinecraftProtoNet.Pathfinding.Goals;

/// <summary>
/// Represents a pathfinding goal that the bot is trying to reach.
/// Based on Baritone's Goal interface.
/// </summary>
public interface IGoal
{
    /// <summary>
    /// Returns whether the given position is within the goal.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="z">Z coordinate</param>
    /// <returns>True if the position satisfies the goal</returns>
    bool IsInGoal(int x, int y, int z);

    /// <summary>
    /// Calculates the heuristic cost estimate from the given position to the goal.
    /// This should never overestimate the actual cost for A* to find optimal paths.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="z">Z coordinate</param>
    /// <returns>Estimated cost in ticks to reach the goal</returns>
    double Heuristic(int x, int y, int z);
}
