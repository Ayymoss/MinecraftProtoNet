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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/selection/ISelectionManager.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Selection;

/// <summary>
/// The selection manager handles setting Baritone's selections.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/selection/ISelectionManager.java
/// </summary>
public interface ISelectionManager
{
    /// <summary>
    /// Adds a new selection. The added selection is returned.
    /// </summary>
    ISelection AddSelection(ISelection selection);

    /// <summary>
    /// Adds a new selection constructed from the given block positions.
    /// </summary>
    ISelection AddSelection(BetterBlockPos pos1, BetterBlockPos pos2);

    /// <summary>
    /// Removes the selection from the current selections.
    /// </summary>
    ISelection? RemoveSelection(ISelection selection);

    /// <summary>
    /// Removes all selections.
    /// </summary>
    IReadOnlyList<ISelection> RemoveAllSelections();

    /// <summary>
    /// Gets the current selections, sorted from oldest to newest.
    /// </summary>
    IReadOnlyList<ISelection> GetSelections();

    /// <summary>
    /// For anything expecting only one selection, this method is provided.
    /// </summary>
    ISelection? GetOnlySelection();

    /// <summary>
    /// This method will always return the last selection.
    /// </summary>
    ISelection? GetLastSelection();

    /// <summary>
    /// Replaces the specified selection with one expanded in the specified direction.
    /// </summary>
    ISelection Expand(ISelection selection, int direction, int blocks);

    /// <summary>
    /// Replaces the specified selection with one contracted in the specified direction.
    /// </summary>
    ISelection Contract(ISelection selection, int direction, int blocks);

    /// <summary>
    /// Replaces the specified selection with one shifted in the specified direction.
    /// </summary>
    ISelection Shift(ISelection selection, int direction, int blocks);
}

