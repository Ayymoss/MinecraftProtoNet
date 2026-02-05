/*
 * This file is part of Baritone.
 *
 * Baritone is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Baritone is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with Baritone.  If not, see <https://www.gnu.org/licenses/>.
 *
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/PathNode.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// A node in the path, containing the cost and steps to get to it.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/PathNode.java
/// </summary>
public sealed class PathNode
{
    /// <summary>
    /// The position of this node
    /// </summary>
    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    /// <summary>
    /// Cached, should always be equal to goal.heuristic(pos)
    /// </summary>
    public readonly double EstimatedCostToGoal;

    /// <summary>
    /// Total cost of getting from start to here.
    /// Mutable and changed by PathFinder
    /// </summary>
    public double Cost;

    /// <summary>
    /// Should always be equal to estimatedCostToGoal + cost.
    /// Mutable and changed by PathFinder
    /// </summary>
    public double CombinedCost;

    /// <summary>
    /// In the graph search, what previous node contributed to the cost.
    /// Mutable and changed by PathFinder
    /// </summary>
    public PathNode? Previous;

    /// <summary>
    /// Where is this node in the array flattenization of the binary heap? Needed for decrease-key operations.
    /// </summary>
    public int HeapPosition;

    public PathNode(int x, int y, int z, Goal goal)
    {
        Previous = null;
        Cost = ActionCosts.CostInf;
        EstimatedCostToGoal = goal.Heuristic(x, y, z);
        if (double.IsNaN(EstimatedCostToGoal))
        {
            throw new InvalidOperationException(
                $"{goal} calculated implausible heuristic NaN at {x} {y} {z}");
        }
        HeapPosition = -1;
        X = x;
        Y = y;
        Z = z;
    }

    public bool IsOpen()
    {
        return HeapPosition != -1;
    }

    public override int GetHashCode()
    {
        return (int)BetterBlockPos.LongHash(X, Y, Z);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not PathNode other)
        {
            return false;
        }
        return X == other.X && Y == other.Y && Z == other.Z;
    }
}

