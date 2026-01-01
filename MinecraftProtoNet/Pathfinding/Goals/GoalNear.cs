namespace MinecraftProtoNet.Pathfinding.Goals;

/// <summary>
/// A goal that represents getting within a certain range of a target position.
/// The goal is satisfied when the distance to the target is less than or equal to the range.
/// </summary>
public class GoalNear : IGoal
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public int RangeSquared { get; }

    /// <summary>
    /// Creates a goal to get within range of a target position.
    /// </summary>
    /// <param name="x">Target X coordinate</param>
    /// <param name="y">Target Y coordinate</param>
    /// <param name="z">Target Z coordinate</param>
    /// <param name="range">Maximum distance from target (in blocks)</param>
    public GoalNear(int x, int y, int z, int range)
    {
        X = x;
        Y = y;
        Z = z;
        RangeSquared = range * range;
    }

    /// <inheritdoc />
    public bool IsInGoal(int x, int y, int z)
    {
        var dx = x - X;
        var dy = y - Y;
        var dz = z - Z;
        return dx * dx + dy * dy + dz * dz <= RangeSquared;
    }

    /// <inheritdoc />
    public double Heuristic(int x, int y, int z)
    {
        // Baritone style: GoalNear uses GoalBlock's heuristic logic.
        // It pulls towards the center even when within range.
        return GoalBlock.CalculateHeuristic(x - X, y - Y, z - Z);
    }

    public override string ToString() => $"GoalNear({X}, {Y}, {Z}, range={Math.Sqrt(RangeSquared):F0})";
}
