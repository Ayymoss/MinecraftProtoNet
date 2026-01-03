using MinecraftProtoNet.Pathfinding;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// The result of a calculated movement, with destination x, y, z, and the cost of performing the movement.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/MutableMoveResult.java
/// </summary>
public sealed class MutableMoveResult
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public double Cost { get; set; }

    public MutableMoveResult()
    {
        Reset();
    }

    /// <summary>
    /// Resets the result to default values (0, 0, 0, CostInf).
    /// </summary>
    public void Reset()
    {
        X = 0;
        Y = 0;
        Z = 0;
        Cost = ActionCosts.CostInf;
    }
}

