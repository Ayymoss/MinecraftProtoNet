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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/Path.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MovementImpl = MinecraftProtoNet.Baritone.Pathfinding.Movement.Movement;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// A node based implementation of IPath.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/Path.java
/// </summary>
internal class Path : IPath
{
    private readonly BetterBlockPos _start;
    private readonly BetterBlockPos _end;
    private readonly List<BetterBlockPos> _path;
    private readonly List<IMovement> _movements;
    private readonly List<PathNode> _nodes;
    private readonly Goal _goal;
    private readonly int _numNodes;
    private readonly CalculationContext _context;
    private volatile bool _verified;

    public Path(BetterBlockPos realStart, PathNode start, PathNode end, int numNodes, Goal goal, CalculationContext context)
    {
        _end = new BetterBlockPos(end.X, end.Y, end.Z);
        _numNodes = numNodes;
        _movements = new List<IMovement>();
        _goal = goal;
        _context = context;

        PathNode? current = end;
        var tempPath = new List<BetterBlockPos>();
        var tempNodes = new List<PathNode>();
        while (current != null)
        {
            tempNodes.Add(current);
            tempPath.Add(new BetterBlockPos(current.X, current.Y, current.Z));
            current = current.Previous;
        }

        // If the position the player is at is different from the position we told A* to start from,
        // and A* gave us no movements, then add a fake node that will allow a movement to be created
        var startNodePos = new BetterBlockPos(start.X, start.Y, start.Z);
        if (!realStart.Equals(startNodePos) && start.Equals(end))
        {
            _start = realStart;
            var fakeNode = new PathNode(realStart.X, realStart.Y, realStart.Z, goal);
            fakeNode.Cost = 0;
            tempNodes.Add(fakeNode);
            tempPath.Add(realStart);
        }
        else
        {
            _start = startNodePos;
        }

        // Nodes are traversed last to first so we need to reverse the list
        _path = tempPath.AsEnumerable().Reverse().ToList();
        _nodes = tempNodes.AsEnumerable().Reverse().ToList();
    }

    public Goal GetGoal() => _goal;

    private bool AssembleMovements()
    {
        if (_path.Count == 0 || _movements.Count != 0)
        {
            throw new InvalidOperationException("Path must not be empty");
        }
        
        // Always log path assembly to diagnose pathfinding issues
        var pathStr = string.Join(" -> ", _path.Select(p => $"({p.X},{p.Y},{p.Z})"));
        _context.GetBaritone().GetGameEventHandler().LogDirect($"Assembling path (length={_path.Count}): {pathStr}");
        
        for (int i = 0; i < _path.Count - 1; i++)
        {
            double cost = _nodes[i + 1].Cost - _nodes[i].Cost;
            IMovement? move = RunBackwards(_path[i], _path[i + 1], cost);
            if (move == null)
            {
                // Always log movement creation failures
                _context.GetBaritone().GetGameEventHandler().LogDirect($"Failed to create movement from {_path[i]} to {_path[i + 1]}");
                return true;
            }
            else
            {
                // Log if movement destination doesn't match expected
                if (!move.GetDest().Equals(_path[i + 1]))
                {
                    _context.GetBaritone().GetGameEventHandler().LogDirect($"WARNING: Created movement: {_path[i]} -> {move.GetDest()} (expected: {_path[i + 1]})");
                }
                _movements.Add(move);
            }
        }
        return false;
    }

    private IMovement? RunBackwards(BetterBlockPos src, BetterBlockPos dest, double cost)
    {
        foreach (var moves in Moves.Values)
        {
            IMovement move = moves.Apply0(_context, src);
            var moveDest = move.GetDest();
            if (moveDest.Equals(dest))
            {
                // have to calculate the cost at calculation time so we can accurately judge whether a cost increase happened between cached calculation and real execution
                // however, taking into account possible favoring that could skew the node cost, we really want the stricter limit of the two
                // so we take the minimum of the path node cost difference, and the calculated cost
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/calc/Path.java:133-136
                if (move is Movement.Movement movementImpl)
                {
                    // Calculate the cost using the context (this will calculate it if not already done)
                    double calculatedCost = movementImpl.GetCost(_context);
                    movementImpl.Override(Math.Min(calculatedCost, cost));
                }
                return move;
            }
        }
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/calc/Path.java:125
        // Debug logging
        if (Core.Baritone.Settings().DebugPathCompletion.Value)
        {
            // LogDirect($"Movement became impossible during calculation {src} {dest}");
        }
        return null;
    }

    public IPath PostProcess()
    {
        if (_verified)
        {
            throw new InvalidOperationException("Path must not be verified twice");
        }
        _verified = true;
        bool failed = AssembleMovements();
        foreach (var m in _movements)
        {
            if (m is MovementImpl movement)
            {
                movement.CheckLoadedChunk(_context);
            }
        }

        if (failed) // at least one movement became impossible during calculation
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/calc/Path.java:147
            // CutoffPath res = new CutoffPath(this, Movements().Count);
            // For now, return this path (cutoff path implementation can be added later)
            SanityCheck();
            return this;
        }
        SanityCheck();
        return this;
    }

    public IReadOnlyList<IMovement> Movements()
    {
        if (!_verified)
        {
            throw new InvalidOperationException("Path not yet verified");
        }
        return _movements.AsReadOnly();
    }

    public IReadOnlyList<BetterBlockPos> Positions()
    {
        return _path.AsReadOnly();
    }

    public int GetNumNodesConsidered() => _numNodes;

    public BetterBlockPos GetSrc() => _start;

    public BetterBlockPos GetDest() => _end;

    public int Length() => _path.Count;

    public double TicksRemainingFrom(int pathPosition)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/calc/Path.java:178
        // Calculate remaining ticks from path position
        double total = 0;
        for (int i = pathPosition; i < _movements.Count; i++)
        {
            total += _movements[i].GetCost();
        }
        return total;
    }

    public IPath CutoffAtLoadedChunks(object bsi)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/calc/Path.java:184
        // Cutoff path at loaded chunks
        // TODO: Implement when chunk loading system is available
        return this;
    }

    public IPath StaticCutoff(Goal destination)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/calc/Path.java:190
        // Static cutoff at destination
        // TODO: Implement when needed
        return this;
    }

    public void SanityCheck()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/calc/Path.java:196
        // Sanity check path validity
        // Basic checks: path should have at least start and end
        if (_path.Count < 2)
        {
            throw new InvalidOperationException("Path must have at least 2 positions");
        }
        if (!_path[0].Equals(_start))
        {
            throw new InvalidOperationException("Path start does not match");
        }
        if (!_path[_path.Count - 1].Equals(_end))
        {
            throw new InvalidOperationException("Path end does not match");
        }
    }
}

