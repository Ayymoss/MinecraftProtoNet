using MinecraftProtoNet.Pathfinding.Calc;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// Result of a path calculation.
/// </summary>
public enum PathCalculationResultType
{
    /// <summary>Path found that reaches the goal.</summary>
    Success,
    /// <summary>Partial path found (best effort).</summary>
    PartialSuccess,
    /// <summary>No path could be found.</summary>
    Failure,
    /// <summary>Calculation was cancelled.</summary>
    Cancelled,
    /// <summary>Calculation timed out.</summary>
    Timeout
}

/// <summary>
/// Interface for pathfinders to allow mocking in tests.
/// </summary>
public interface IPathFinder
{
    /// <summary>
    /// Calculates a path with the given timeout.
    /// </summary>
    /// <param name="primaryTimeoutMs">Timeout for full path calculation</param>
    /// <param name="failureTimeoutMs">Extended timeout when no path found yet</param>
    /// <returns>Path calculation result</returns>
    (PathCalculationResultType Type, Path? Path) Calculate(long primaryTimeoutMs, long failureTimeoutMs);

    /// <summary>
    /// Requests cancellation of the current calculation.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Gets the best path found so far during calculation (for pause logic).
    /// Returns null if no valid partial path exists yet.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java:186-188
    /// </summary>
    Path? BestPathSoFar();
}
