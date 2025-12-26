using MinecraftProtoNet.Pathfinding.Goals;

namespace MinecraftProtoNet.Pathfinding.Calc;

/// <summary>
/// Represents a calculated path from start to goal.
/// Based on Baritone's IPath interface.
/// </summary>
public class Path
{
    /// <summary>
    /// List of positions along the path.
    /// </summary>
    public IReadOnlyList<(int X, int Y, int Z)> Positions { get; }

    /// <summary>
    /// The starting position.
    /// </summary>
    public (int X, int Y, int Z) Start => Positions[0];

    /// <summary>
    /// The destination position.
    /// </summary>
    public (int X, int Y, int Z) Destination => Positions[^1];

    /// <summary>
    /// The goal that was used for pathfinding.
    /// </summary>
    public IGoal Goal { get; }

    /// <summary>
    /// Number of nodes considered during pathfinding.
    /// </summary>
    public int NumNodesConsidered { get; }

    /// <summary>
    /// Whether the path reaches the goal.
    /// </summary>
    public bool ReachesGoal { get; }

    /// <summary>
    /// Total length of the path in blocks.
    /// </summary>
    public int Length => Positions.Count;

    public Path(IReadOnlyList<(int X, int Y, int Z)> positions, IGoal goal, int numNodesConsidered, bool reachesGoal)
    {
        Positions = positions;
        Goal = goal;
        NumNodesConsidered = numNodesConsidered;
        ReachesGoal = reachesGoal;
    }

    /// <summary>
    /// Creates a path by backtracking from the end node to the start.
    /// </summary>
    public static Path FromEndNode(PathNode endNode, IGoal goal, int numNodesConsidered)
    {
        var positions = new List<(int, int, int)>();
        var current = endNode;

        while (current != null)
        {
            positions.Add((current.X, current.Y, current.Z));
            current = current.Previous;
        }

        positions.Reverse();

        var reachesGoal = goal.IsInGoal(endNode.X, endNode.Y, endNode.Z);
        return new Path(positions, goal, numNodesConsidered, reachesGoal);
    }

    public override string ToString()
    {
        return $"Path(length={Length}, nodes={NumNodesConsidered}, reachesGoal={ReachesGoal})";
    }
}
