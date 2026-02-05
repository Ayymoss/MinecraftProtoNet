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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/calc/IPath.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Pathing.Calc;

/// <summary>
/// Interface for a calculated path.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/calc/IPath.java
/// </summary>
public interface IPath
{
    /// <summary>
    /// Ordered list of movements to carry out.
    /// </summary>
    IReadOnlyList<IMovement> Movements();

    /// <summary>
    /// All positions along the way.
    /// </summary>
    IReadOnlyList<BetterBlockPos> Positions();

    /// <summary>
    /// This path is actually going to be executed in the world. Do whatever additional processing is required.
    /// </summary>
    IPath PostProcess();

    /// <summary>
    /// Returns the number of positions in this path.
    /// </summary>
    int Length();

    /// <summary>
    /// Gets the goal that this path was calculated towards.
    /// </summary>
    Goal GetGoal();

    /// <summary>
    /// Returns the number of nodes that were considered during calculation.
    /// </summary>
    int GetNumNodesConsidered();

    /// <summary>
    /// Returns the start position of this path.
    /// </summary>
    BetterBlockPos GetSrc();

    /// <summary>
    /// Returns the end position of this path.
    /// </summary>
    BetterBlockPos GetDest();

    /// <summary>
    /// Returns the estimated number of ticks to complete the path from the given node index.
    /// </summary>
    double TicksRemainingFrom(int pathPosition);

    /// <summary>
    /// Cuts off this path at the loaded chunk border.
    /// </summary>
    IPath CutoffAtLoadedChunks(object bsi);

    /// <summary>
    /// Cuts off this path using the min length and cutoff factor settings.
    /// </summary>
    IPath StaticCutoff(Goal destination);

    /// <summary>
    /// Performs a series of checks to ensure that the assembly of the path went as expected.
    /// </summary>
    void SanityCheck();
}

