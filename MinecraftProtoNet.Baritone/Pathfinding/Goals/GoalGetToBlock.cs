using MinecraftProtoNet.Pathfinding.Goals;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Goal to get adjacent to a block, not into it. Useful for chests, crafting tables, etc.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalGetToBlock.java
/// </summary>
public class GoalGetToBlock : IGoal
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public GoalGetToBlock(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <inheritdoc />
    public bool IsInGoal(int x, int y, int z)
    {
        int xDiff = x - X;
        int yDiff = y - Y;
        int zDiff = z - Z;
        
        // Adjacent (manhattan distance <= 1) counts as in goal
        // Baritone: Math.abs(xDiff) + Math.abs(yDiff < 0 ? yDiff + 1 : yDiff) + Math.abs(zDiff) <= 1
        return Math.Abs(xDiff) + Math.Abs(yDiff < 0 ? yDiff + 1 : yDiff) + Math.Abs(zDiff) <= 1;
    }

    /// <inheritdoc />
    public double Heuristic(int x, int y, int z)
    {
        int xDiff = x - X;
        int yDiff = y - Y;
        int zDiff = z - Z;
        
        // Adjust yDiff like Baritone does
        return GoalBlock.CalculateHeuristic(xDiff, yDiff < 0 ? yDiff + 1 : yDiff, zDiff);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not GoalGetToBlock other) return false;
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    public override int GetHashCode() => HashCode.Combine(X, Y, Z) * -49639096;

    public override string ToString() => $"GoalGetToBlock({X}, {Y}, {Z})";
}
