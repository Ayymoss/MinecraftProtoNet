using MinecraftProtoNet.Pathfinding.Goals;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// A composite of many goals, any one of which satisfies the composite.
/// For example, a GoalComposite of block goals for every oak log would result
/// in pathing to the easiest oak log to reach.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalComposite.java
/// </summary>
public class GoalComposite : IGoal
{
    /// <summary>
    /// An array of goals where any one of them must be satisfied.
    /// </summary>
    public IGoal[] Goals { get; }

    public GoalComposite(params IGoal[] goals)
    {
        Goals = goals;
    }

    /// <inheritdoc />
    public bool IsInGoal(int x, int y, int z)
    {
        foreach (var goal in Goals)
        {
            if (goal.IsInGoal(x, y, z))
            {
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc />
    public double Heuristic(int x, int y, int z)
    {
        var min = double.MaxValue;
        foreach (var goal in Goals)
        {
            min = Math.Min(min, goal.Heuristic(x, y, z));
        }
        return min;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not GoalComposite other) return false;
        if (Goals.Length != other.Goals.Length) return false;
        for (int i = 0; i < Goals.Length; i++)
        {
            if (!Goals[i].Equals(other.Goals[i])) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (var goal in Goals)
            {
                hash = hash * 31 + goal.GetHashCode();
            }
            return hash;
        }
    }

    public override string ToString() => $"GoalComposite[{string.Join(", ", (object[])Goals)}]";
}
