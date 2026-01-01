namespace MinecraftProtoNet.Pathfinding.Calc;

/// <summary>
/// Represents a node in the A* pathfinding graph.
/// Based on Baritone's PathNode.java.
/// </summary>
public class PathNode
{
    /// <summary>
    /// X coordinate of this node.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Y coordinate of this node.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Z coordinate of this node.
    /// </summary>
    public int Z { get; }

    /// <summary>
    /// Hash code for quick lookup.
    /// </summary>
    public long HashCode { get; }

    /// <summary>
    /// The g-cost: actual cost from the start node to this node.
    /// </summary>
    public double Cost { get; set; } = ActionCosts.CostInf;

    /// <summary>
    /// The h-cost: estimated cost from this node to the goal.
    /// </summary>
    public double EstimatedCostToGoal { get; set; }

    /// <summary>
    /// The f-cost: combined cost (g + h).
    /// </summary>
    public double CombinedCost { get; set; } = ActionCosts.CostInf;

    /// <summary>
    /// Parent node in the path.
    /// </summary>
    public PathNode? Previous { get; set; }

    /// <summary>
    /// Index in the binary heap for O(log n) updates.
    /// -1 means not in the open set.
    /// </summary>
    public int HeapIndex { get; set; } = -1;

    public PathNode(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
        HashCode = CalculateHash(x, y, z);
    }

    /// <summary>
    /// Returns whether this node is currently in the open set.
    /// </summary>
    public bool IsOpen() => HeapIndex >= 0;

    /// <summary>
    /// Calculates a unique hash for the given coordinates.
    /// Uses long to ensure unique values for all possible Minecraft coordinates.
    /// </summary>
    public static long CalculateHash(int x, int y, int z)
    {
        // Pack coordinates into a long (Minecraft's BetterBlockPos.longHash approach)
        // Y is limited to 0-319 (or -64 to 320 with 1.18+), so 10 bits is enough
        // X and Z can be +/- 30 million, so need 26 bits each
        return ((long)(x + 30000000) << 36) | ((long)(z + 30000000) << 10) | (y + 64);
    }

    public override bool Equals(object? obj)
    {
        if (obj is PathNode other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }
        return false;
    }

    public override int GetHashCode() => (int)HashCode;

    public override string ToString() => $"PathNode({X}, {Y}, {Z}, cost={Cost:F2})";
}
