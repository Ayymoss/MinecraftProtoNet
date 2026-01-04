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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/openset/IOpenSet.java
 */

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc.OpenSet;

/// <summary>
/// An open set for A* or similar graph search algorithm.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/openset/IOpenSet.java
/// </summary>
public interface IOpenSet
{
    /// <summary>
    /// Inserts the specified node into the heap.
    /// </summary>
    void Insert(PathNode node);

    /// <summary>
    /// Returns true if the heap has no elements; false otherwise.
    /// </summary>
    bool IsEmpty();

    /// <summary>
    /// Removes and returns the minimum element in the heap.
    /// </summary>
    PathNode RemoveLowest();

    /// <summary>
    /// A faster path has been found to this node, decreasing its cost. Perform a decrease-key operation.
    /// </summary>
    void Update(PathNode node);
}

