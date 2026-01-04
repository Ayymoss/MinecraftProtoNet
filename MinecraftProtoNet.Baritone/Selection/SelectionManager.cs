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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/selection/SelectionManager.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Selection;

namespace MinecraftProtoNet.Baritone.Selection;

/// <summary>
/// Selection manager implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/selection/SelectionManager.java
/// </summary>
public class SelectionManager : ISelectionManager
{
    private readonly IBaritone _baritone;
    private readonly List<ISelection> _selections = new();

    public SelectionManager(IBaritone baritone)
    {
        _baritone = baritone;
    }

    public ISelection AddSelection(ISelection selection)
    {
        _selections.Add(selection);
        return selection;
    }

    public ISelection AddSelection(Api.Utils.BetterBlockPos pos1, Api.Utils.BetterBlockPos pos2)
    {
        // Will be implemented when Selection class is created
        throw new NotImplementedException();
    }

    public ISelection? RemoveSelection(ISelection selection)
    {
        return _selections.Remove(selection) ? selection : null;
    }

    public IReadOnlyList<ISelection> RemoveAllSelections()
    {
        var result = _selections.ToList();
        _selections.Clear();
        return result;
    }

    public IReadOnlyList<ISelection> GetSelections() => _selections;

    public ISelection? GetOnlySelection() => _selections.Count == 1 ? _selections[0] : null;

    public ISelection? GetLastSelection() => _selections.Count > 0 ? _selections[^1] : null;

    public ISelection Expand(ISelection selection, int direction, int blocks)
    {
        return selection.Expand(direction, blocks);
    }

    public ISelection Contract(ISelection selection, int direction, int blocks)
    {
        return selection.Contract(direction, blocks);
    }

    public ISelection Shift(ISelection selection, int direction, int blocks)
    {
        return selection.Shift(direction, blocks);
    }
}

