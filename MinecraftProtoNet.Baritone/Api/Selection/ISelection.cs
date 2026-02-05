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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/selection/ISelection.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core.Physics.Shapes;

namespace MinecraftProtoNet.Baritone.Api.Selection;

/// <summary>
/// A selection is an immutable object representing the current selection.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/selection/ISelection.java
/// </summary>
public interface ISelection
{
    /// <summary>
    /// Gets the first corner of this selection.
    /// </summary>
    BetterBlockPos Pos1();

    /// <summary>
    /// Gets the second corner of this selection.
    /// </summary>
    BetterBlockPos Pos2();

    /// <summary>
    /// Gets the position with the lowest x, y, and z position in the selection.
    /// </summary>
    BetterBlockPos Min();

    /// <summary>
    /// Gets the opposite corner from the min.
    /// </summary>
    BetterBlockPos Max();

    /// <summary>
    /// Gets the size of this selection.
    /// </summary>
    (int X, int Y, int Z) Size();

    /// <summary>
    /// Gets an AABB encompassing all blocks in this selection.
    /// </summary>
    AABB Aabb();

    /// <summary>
    /// Returns a new selection expanded in the specified direction by the specified number of blocks.
    /// </summary>
    ISelection Expand(int direction, int blocks);

    /// <summary>
    /// Returns a new selection contracted in the specified direction by the specified number of blocks.
    /// </summary>
    ISelection Contract(int direction, int blocks);

    /// <summary>
    /// Returns a new selection shifted in the specified direction by the specified number of blocks.
    /// </summary>
    ISelection Shift(int direction, int blocks);
}

