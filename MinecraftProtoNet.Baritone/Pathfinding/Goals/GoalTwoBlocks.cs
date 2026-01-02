using MinecraftProtoNet.Pathfinding.Goals;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Goal satisfied when standing at the target block or one block below.
/// Useful for mining a block - being at the block or just below it both work.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalTwoBlocks.java
/// </summary>
public class GoalTwoBlocks : IGoal
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public GoalTwoBlocks(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <inheritdoc />
    public bool IsInGoal(int x, int y, int z)
    {
        // Satisfied if at (x, y, z) or (x, y-1, z)
        return x == X && (y == Y || y == Y - 1) && z == Z;
    }

    /// <inheritdoc />
    public double Heuristic(int x, int y, int z)
    {
        int xDiff = x - X;
        int yDiff = y - Y;
        int zDiff = z - Z;
        
        // Adjust yDiff to account for the two-block acceptance
        return GoalBlock.CalculateHeuristic(xDiff, yDiff < 0 ? yDiff + 1 : yDiff, zDiff);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not GoalTwoBlocks other) return false;
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    public override int GetHashCode() => HashCode.Combine(X, Y, Z) * 516508351;

    public override string ToString() => $"GoalTwoBlocks({X}, {Y}, {Z})";
}
