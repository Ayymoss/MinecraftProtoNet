using MinecraftProtoNet.Pathfinding.Goals;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Inverts any goal. The heuristic is negated so the bot tries to get AWAY from the goal.
/// IsInGoal always returns false since you can never truly "achieve" an inverted goal.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalInverted.java
/// </summary>
public class GoalInverted : IGoal
{
    /// <summary>
    /// The original goal being inverted.
    /// </summary>
    public IGoal Origin { get; }

    public GoalInverted(IGoal origin)
    {
        Origin = origin;
    }

    /// <inheritdoc />
    /// <remarks>Always returns false - you can never reach an inverted goal.</remarks>
    public bool IsInGoal(int x, int y, int z)
    {
        return false;
    }

    /// <inheritdoc />
    /// <remarks>Negates the original heuristic to make the bot move away.</remarks>
    public double Heuristic(int x, int y, int z)
    {
        return -Origin.Heuristic(x, y, z);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not GoalInverted other) return false;
        return Origin.Equals(other.Origin);
    }

    public override int GetHashCode() => Origin.GetHashCode() * 495796690;

    public override string ToString() => $"GoalInverted({Origin})";
}
