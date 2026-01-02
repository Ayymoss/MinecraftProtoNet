using MinecraftProtoNet.Pathfinding.Goals;
namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// A goal that represents reaching a specific X/Z coordinate at any Y level.
/// Useful for long-distance navigation where the exact height doesn't matter.
/// </summary>
public class GoalXZ : IGoal
{
    public int X { get; }
    public int Z { get; }

    public GoalXZ(int x, int z)
    {
        X = x;
        Z = z;
    }

    /// <inheritdoc />
    public bool IsInGoal(int x, int y, int z)
    {
        return x == X && z == Z;
    }

    /// <inheritdoc />
    public double Heuristic(int x, int y, int z)
    {
        // Baritone style: use GoalBlock.CalculateHeuristic with 0 Y difference
        return GoalBlock.CalculateHeuristic(x - X, 0, z - Z);
    }

    public override string ToString() => $"GoalXZ({X}, {Z})";
}
