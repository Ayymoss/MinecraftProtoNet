using System.Collections.Generic;
using MinecraftProtoNet.Pathfinding.Calc;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// Applies cost multipliers to favor or avoid certain positions during pathfinding.
/// Used to avoid backtracking and apply mob avoidance.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/Favoring.java
/// </summary>
public class Favoring
{
    private readonly Dictionary<long, double> _favorings;
    private const double DefaultMultiplier = 1.0;

    /// <summary>
    /// Creates a favoring instance from a previous path (backtrack avoidance).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/Favoring.java:39-46
    /// </summary>
    /// <param name="previous">Previous path to avoid backtracking on</param>
    /// <param name="context">Calculation context with backtrackCostFavoringCoefficient</param>
    public Favoring(Path? previous, CalculationContext context)
    {
        _favorings = new Dictionary<long, double>();
        double coeff = context.BacktrackCostFavoringCoefficient;
        
        if (coeff != 1.0 && previous != null)
        {
            // Apply backtrack cost multiplier to all positions in previous path
            foreach (var pos in previous.Positions)
            {
                var hash = PathNode.CalculateHash(pos.X, pos.Y, pos.Z);
                _favorings[hash] = coeff;
            }
        }
    }

    /// <summary>
    /// Checks if this favoring instance is empty (no multipliers applied).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/Favoring.java:48-50
    /// </summary>
    public bool IsEmpty => _favorings.Count == 0;

    /// <summary>
    /// Calculates the cost multiplier for a given position hash.
    /// Returns 1.0 if no multiplier is set for this position.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/Favoring.java:52-54
    /// </summary>
    /// <param name="hash">Position hash (from PathNode.CalculateHash)</param>
    /// <returns>Cost multiplier (default 1.0)</returns>
    public double Calculate(long hash)
    {
        return _favorings.TryGetValue(hash, out var multiplier) ? multiplier : DefaultMultiplier;
    }
}

