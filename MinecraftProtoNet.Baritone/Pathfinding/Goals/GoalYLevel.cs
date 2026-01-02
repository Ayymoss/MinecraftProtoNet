using MinecraftProtoNet.Pathfinding;
using MinecraftProtoNet.Pathfinding.Goals;
namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// A goal that represents reaching a specific Y level.
/// The goal is satisfied at any X/Z when at the target Y level.
/// </summary>
public class GoalYLevel : IGoal
{
    public int Y { get; }

    public GoalYLevel(int y)
    {
        Y = y;
    }

    /// <inheritdoc />
    public bool IsInGoal(int x, int y, int z)
    {
        return y == Y;
    }

    /// <inheritdoc />
    public double Heuristic(int x, int y, int z)
    {
        var yDiff = Math.Abs(y - Y);
        
        if (yDiff == 0) return 0;

        if (y < Y)
        {
            // Need to go up
            return yDiff * ActionCosts.JumpOneBlockCost;
        }
        else
        {
            // Need to go down - falling
            return yDiff < ActionCosts.FallNBlocksCost.Length
                ? ActionCosts.FallNBlocksCost[yDiff]
                : ActionCosts.CostInf;
        }
    }

    public override string ToString() => $"GoalYLevel({Y})";
}
