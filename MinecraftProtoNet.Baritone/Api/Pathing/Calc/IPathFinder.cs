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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/calc/IPathFinder.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Pathing.Calc;

/// <summary>
/// Generic path finder interface.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/calc/IPathFinder.java
/// </summary>
public interface IPathFinder
{
    /// <summary>
    /// Gets the goal for this path finder.
    /// </summary>
    Goal GetGoal();

    /// <summary>
    /// Calculate the path in full. Will take several seconds.
    /// </summary>
    /// <param name="primaryTimeout">If a path is found, the path finder will stop after this amount of time</param>
    /// <param name="failureTimeout">If a path isn't found, the path finder will continue for this amount of time</param>
    /// <returns>The final path</returns>
    PathCalculationResult Calculate(long primaryTimeout, long failureTimeout);

    /// <summary>
    /// Intended to be called concurrently with calculatePath from a different thread to tell if it's finished yet.
    /// </summary>
    bool IsFinished();

    /// <summary>
    /// Called for path rendering. Returns a path to the most recent node popped from the open set and considered.
    /// </summary>
    IPath? PathToMostRecentNodeConsidered();

    /// <summary>
    /// The best path so far, according to the most forgiving coefficient heuristic.
    /// </summary>
    IPath? BestPathSoFar();
}

