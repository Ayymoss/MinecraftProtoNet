namespace MinecraftProtoNet.Pathfinding.Goals;

/// <summary>
/// A goal that represents reaching a specific block position.
/// The goal is satisfied when standing on or in the exact block.
/// </summary>
public class GoalBlock : IGoal
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public GoalBlock(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <inheritdoc />
    public bool IsInGoal(int x, int y, int z)
    {
        return x == X && y == Y && z == Z;
    }

    /// <inheritdoc />
    public double Heuristic(int x, int y, int z)
    {
        var xDiff = x - X;
        var yDiff = y - Y;
        var zDiff = z - Z;
        
        // Baritone style: use GoalXZ + GoalYLevel
        return CalculateHeuristic(xDiff, yDiff, zDiff);
    }

    public static double CalculateHeuristic(double xDiff, int yDiff, double zDiff)
    {
        // Y cost
        double vertical = 0;
        if (yDiff > 0) // need to descend
        {
            vertical = yDiff * (ActionCosts.GetFallCost(2) / 2.0);
        }
        else if (yDiff < 0) // need to ascend
        {
            vertical = (-yDiff) * ActionCosts.JumpOneBlockCost;
        }

        // XZ cost
        double x = Math.Abs(xDiff);
        double z = Math.Abs(zDiff);
        double straight = Math.Abs(x - z);
        double diagonal = Math.Min(x, z);
        
        double horizontal = (diagonal * Math.Sqrt(2) + straight) * ActionCosts.WalkOneBlockCost;

        return horizontal + vertical;
    }

    public override string ToString() => $"GoalBlock({X}, {Y}, {Z})";
}
