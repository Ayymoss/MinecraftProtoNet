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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Pathfinding.Calc.OpenSet;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Utils.Pathing;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// The actual A* pathfinding.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java
/// </summary>
public sealed class AStarPathFinder : AbstractNodeCostSearch
{
    private readonly Favoring _favoring;
    private readonly CalculationContext _calcContext;

    public AStarPathFinder(BetterBlockPos realStart, int startX, int startY, int startZ, Goal goal, Favoring favoring, CalculationContext context)
        : base(realStart, startX, startY, startZ, goal, context)
    {
        _favoring = favoring;
        _calcContext = context;
    }

    protected override IPath? Calculate0(long primaryTimeout, long failureTimeout)
    {
        int minY = _calcContext.World.DimensionType.MinY;
        int height = _calcContext.World.DimensionType.Height;
        StartNode = GetNodeAtPosition(StartX, StartY, StartZ, BetterBlockPos.LongHash(StartX, StartY, StartZ));
        StartNode.Cost = 0;
        StartNode.CombinedCost = StartNode.EstimatedCostToGoal;
        var openSet = new BinaryHeapOpenSet();
        openSet.Insert(StartNode);
        double[] bestHeuristicSoFar = new double[Coefficients.Length]; // keep track of the best node by the metric of (estimatedCostToGoal + cost / COEFFICIENTS[i])
        for (int i = 0; i < bestHeuristicSoFar.Length; i++)
        {
            bestHeuristicSoFar[i] = StartNode.EstimatedCostToGoal;
            BestSoFar[i] = StartNode;
        }
        var res = new MutableMoveResult();
        var worldBorder = new BetterWorldBorder(_calcContext.World.WorldBorder);
        long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bool slowPath = Core.Baritone.Settings().SlowPath.Value;
        if (slowPath)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/calc/AStarPathFinder.java:67
            // Debug logging
            if (Core.Baritone.Settings().DebugPathCompletion.Value)
            {
                // LogDirect($"slowPath is on, path timeout will be {Core.Baritone.Settings().SlowPathTimeoutMs.Value}ms instead of {primaryTimeout}ms");
            }
        }
        long primaryTimeoutTime = startTime + (slowPath ? Core.Baritone.Settings().SlowPathTimeoutMs.Value : primaryTimeout);
        long failureTimeoutTime = startTime + (slowPath ? Core.Baritone.Settings().SlowPathTimeoutMs.Value : failureTimeout);
        bool failing = true;
        int numNodes = 0;
        int numMovementsConsidered = 0;
        int numEmptyChunk = 0;
        bool isFavoring = !_favoring.IsEmpty();
        int timeCheckInterval = 1 << 6;
        int pathingMaxChunkBorderFetch = Core.Baritone.Settings().PathingMaxChunkBorderFetch.Value;
        double minimumImprovement = Core.Baritone.Settings().MinimumImprovementRepropagation.Value ? MinImprovement : 0;
        var allMoves = Moves.Values;
        while (!openSet.IsEmpty() && numEmptyChunk < pathingMaxChunkBorderFetch && !CancelRequested)
        {
            if ((numNodes & (timeCheckInterval - 1)) == 0) // only call this once every 64 nodes (about half a millisecond)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - failureTimeoutTime >= 0 || (!failing && now - primaryTimeoutTime >= 0))
                {
                    break;
                }
            }
            if (slowPath)
            {
                Thread.Sleep((int)Core.Baritone.Settings().SlowPathTimeDelayMs.Value);
            }
            PathNode currentNode = openSet.RemoveLowest();
            MostRecentConsidered = currentNode;
            numNodes++;
            if (Goal.IsInGoal(currentNode.X, currentNode.Y, currentNode.Z))
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/calc/AStarPathFinder.java:99
                // Debug logging
                if (Core.Baritone.Settings().DebugPathCompletion.Value)
                {
                    long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime;
                    // LogDirect($"Took {elapsed}ms, {numMovementsConsidered} movements considered");
                }
                return new Path(RealStart, StartNode!, currentNode, numNodes, Goal, _calcContext);
            }
            foreach (var moves in allMoves)
            {
                int newX = currentNode.X + moves.XOffset;
                int newZ = currentNode.Z + moves.ZOffset;
                if ((newX >> 4 != currentNode.X >> 4 || newZ >> 4 != currentNode.Z >> 4) && !_calcContext.IsLoaded(newX, newZ))
                {
                    // only need to check if the destination is a loaded chunk if it's in a different chunk than the start of the movement
                    if (!moves.DynamicXZ) // only increment the counter if the movement would have gone out of bounds guaranteed
                    {
                        numEmptyChunk++;
                    }
                    continue;
                }
                if (!moves.DynamicXZ && !worldBorder.EntirelyContains(newX, newZ))
                {
                    continue;
                }
                if (currentNode.Y + moves.YOffset > height || currentNode.Y + moves.YOffset < minY)
                {
                    continue;
                }
                res.Reset();
                moves.Apply(_calcContext, currentNode.X, currentNode.Y, currentNode.Z, res);
                numMovementsConsidered++;
                double actionCost = res.Cost;
                if (actionCost >= ActionCosts.CostInf)
                {
                    continue;
                }
                if (actionCost <= 0 || double.IsNaN(actionCost))
                {
                    throw new InvalidOperationException(
                        $"{moves} from {currentNode.X} {currentNode.Y} {currentNode.Z} calculated implausible cost {actionCost}");
                }
                // check destination after verifying it's not COST_INF -- some movements return COST_INF without adjusting the destination
                if (moves.DynamicXZ && !worldBorder.EntirelyContains(res.X, res.Z))
                {
                    continue;
                }
                if (!moves.DynamicXZ && (res.X != newX || res.Z != newZ))
                {
                    throw new InvalidOperationException(
                        $"{moves} from {currentNode.X} {currentNode.Y} {currentNode.Z} ended at x z {res.X} {res.Z} instead of {newX} {newZ}");
                }
                if (!moves.DynamicY && res.Y != currentNode.Y + moves.YOffset)
                {
                    throw new InvalidOperationException(
                        $"{moves} from {currentNode.X} {currentNode.Y} {currentNode.Z} ended at y {res.Y} instead of {currentNode.Y + moves.YOffset}");
                }
                long hashCode = BetterBlockPos.LongHash(res.X, res.Y, res.Z);
                if (isFavoring)
                {
                    // see issue #18
                    actionCost *= _favoring.Calculate(hashCode);
                }
                PathNode neighbor = GetNodeAtPosition(res.X, res.Y, res.Z, hashCode);
                double tentativeCost = currentNode.Cost + actionCost;
                if (neighbor.Cost - tentativeCost > minimumImprovement)
                {
                    neighbor.Previous = currentNode;
                    neighbor.Cost = tentativeCost;
                    neighbor.CombinedCost = tentativeCost + neighbor.EstimatedCostToGoal;
                    if (neighbor.IsOpen())
                    {
                        openSet.Update(neighbor);
                    }
                    else
                    {
                        openSet.Insert(neighbor); // don't double count, don't insert into open set if it's already there
                    }
                    for (int i = 0; i < Coefficients.Length; i++)
                    {
                        double heuristic = neighbor.EstimatedCostToGoal + neighbor.Cost / Coefficients[i];
                        if (bestHeuristicSoFar[i] - heuristic > minimumImprovement)
                        {
                            bestHeuristicSoFar[i] = heuristic;
                            BestSoFar[i] = neighbor;
                            if (failing && GetDistFromStartSq(neighbor) > MinDistPath * MinDistPath)
                            {
                                failing = false;
                            }
                        }
                    }
                }
            }
        }
        if (CancelRequested)
        {
            return null;
        }
        Console.WriteLine($"{numMovementsConsidered} movements considered");
        Console.WriteLine($"Open set size: {openSet.Size()}");
        Console.WriteLine($"PathNode map size: {MapSize()}");
        Console.WriteLine($"{(int)(numNodes * 1.0 / ((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime) / 1000.0))} nodes per second");
        IPath? result = BestSoFarInternal(true, numNodes);
        if (result != null)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/calc/AStarPathFinder.java:199
            // Debug logging
            if (Core.Baritone.Settings().DebugPathCompletion.Value)
            {
                long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime;
                // LogDirect($"Took {elapsed}ms, {numMovementsConsidered} movements considered");
            }
        }
        return result;
    }
}

