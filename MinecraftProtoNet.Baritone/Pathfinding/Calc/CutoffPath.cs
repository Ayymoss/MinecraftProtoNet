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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/CutoffPath.java
 *          and: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/PathBase.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Utils;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// Shared IPath logic mirroring Java's PathBase (cutoff helpers) and IPath default sanityCheck.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/PathBase.java
///            baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/calc/IPath.java:150
/// </summary>
internal static class PathBaseLogic
{
    // Reference: PathBase.java:31-43
    public static IPath CutoffAtLoadedChunks(IPath self, object bsi0)
    {
        if (!Core.Baritone.Settings().CutoffAtLoadBoundary.Value)
        {
            return self;
        }
        var bsi = (BlockStateInterface)bsi0;
        var positions = self.Positions();
        for (int i = 0; i < positions.Count; i++)
        {
            var pos = positions[i];
            if (!bsi.WorldContainsLoadedChunk(pos.X, pos.Z))
            {
                return new CutoffPath(self, i);
            }
        }
        return self;
    }

    // Reference: PathBase.java:45-57
    public static IPath StaticCutoff(IPath self, Goal? destination)
    {
        int min = Core.Baritone.Settings().PathCutoffMinimumLength.Value;
        if (self.Length() < min)
        {
            return self;
        }
        if (destination == null || destination.IsInGoal(self.GetDest()))
        {
            return self;
        }
        double factor = Core.Baritone.Settings().PathCutoffFactor.Value;
        int newLength = (int)((self.Length() - min) * factor) + min - 1;
        return new CutoffPath(self, newLength);
    }

    // Reference: IPath.java:150-178
    public static void SanityCheck(IPath self)
    {
        var path = self.Positions();
        var movements = self.Movements();
        if (path.Count < 2)
        {
            throw new InvalidOperationException("Path must have at least 2 positions");
        }
        if (!self.GetSrc().Equals(path[0]))
        {
            throw new InvalidOperationException("Start node does not equal first path element");
        }
        if (!self.GetDest().Equals(path[^1]))
        {
            throw new InvalidOperationException("End node does not equal last path element");
        }
        if (path.Count != movements.Count + 1)
        {
            throw new InvalidOperationException($"Size of path array is unexpected: {path.Count} positions vs {movements.Count} movements");
        }
        var seenSoFar = new HashSet<BetterBlockPos>();
        for (int i = 0; i < path.Count - 1; i++)
        {
            var src = path[i];
            var dest = path[i + 1];
            var movement = movements[i];
            if (!src.Equals(movement.GetSrc()))
            {
                throw new InvalidOperationException($"Path source is not equal to the movement source ({src} vs {movement.GetSrc()})");
            }
            if (!dest.Equals(movement.GetDest()))
            {
                throw new InvalidOperationException($"Path destination is not equal to the movement destination ({dest} vs {movement.GetDest()})");
            }
            if (seenSoFar.Contains(src))
            {
                throw new InvalidOperationException($"Path doubles back on itself, making a loop {src}");
            }
            seenSoFar.Add(src);
        }
    }
}

/// <summary>
/// A view onto a sub-range of another path. Used by the loaded-chunk and static cutoffs.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/CutoffPath.java
/// </summary>
internal sealed class CutoffPath : IPath
{
    private readonly List<BetterBlockPos> _path;
    private readonly List<IMovement> _movements;
    private readonly int _numNodes;
    private readonly Goal _goal;

    public CutoffPath(IPath prev, int firstPositionToInclude, int lastPositionToInclude)
    {
        // path = positions[first .. last] inclusive; movements = movements[first .. last) (one fewer)
        _path = new List<BetterBlockPos>();
        var prevPositions = prev.Positions();
        for (int i = firstPositionToInclude; i <= lastPositionToInclude; i++)
        {
            _path.Add(prevPositions[i]);
        }
        _movements = new List<IMovement>();
        var prevMovements = prev.Movements();
        for (int i = firstPositionToInclude; i < lastPositionToInclude; i++)
        {
            _movements.Add(prevMovements[i]);
        }
        _numNodes = prev.GetNumNodesConsidered();
        _goal = prev.GetGoal();
        SanityCheck();
    }

    public CutoffPath(IPath prev, int lastPositionToInclude) : this(prev, 0, lastPositionToInclude)
    {
    }

    public Goal GetGoal() => _goal;

    public IReadOnlyList<IMovement> Movements() => _movements.AsReadOnly();

    public IReadOnlyList<BetterBlockPos> Positions() => _path.AsReadOnly();

    public int GetNumNodesConsidered() => _numNodes;

    public BetterBlockPos GetSrc() => _path[0];

    public BetterBlockPos GetDest() => _path[^1];

    public int Length() => _path.Count;

    public double TicksRemainingFrom(int pathPosition)
    {
        double total = 0;
        for (int i = pathPosition; i < _movements.Count; i++)
        {
            total += _movements[i].GetCost();
        }
        return total;
    }

    // A cutoff path is produced from an already-processed path. Reference: IPath.java:57 default throws.
    public IPath PostProcess() => throw new InvalidOperationException("CutoffPath is already post-processed");

    public IPath CutoffAtLoadedChunks(object bsi) => PathBaseLogic.CutoffAtLoadedChunks(this, bsi);

    public IPath StaticCutoff(Goal destination) => PathBaseLogic.StaticCutoff(this, destination);

    public void SanityCheck() => PathBaseLogic.SanityCheck(this);
}
