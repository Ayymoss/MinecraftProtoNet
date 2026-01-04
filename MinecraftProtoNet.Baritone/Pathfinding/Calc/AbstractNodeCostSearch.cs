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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// Any pathfinding algorithm that keeps track of nodes recursively by their cost (e.g. A*, dijkstra).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java
/// </summary>
public abstract class AbstractNodeCostSearch : IPathFinder
{
    protected readonly BetterBlockPos RealStart;
    protected readonly int StartX;
    protected readonly int StartY;
    protected readonly int StartZ;

    protected readonly Goal Goal;

    private readonly CalculationContext Context;

    /// <summary>
    /// Map from position hash code to PathNode.
    /// </summary>
    private readonly Dictionary<long, PathNode> Map;

    protected PathNode? StartNode;

    protected PathNode? MostRecentConsidered;

    protected readonly PathNode?[] BestSoFar = new PathNode[Coefficients.Length];

    private volatile bool _isFinished;

    protected bool CancelRequested;

    /// <summary>
    /// This is really complicated and hard to explain. I wrote a comment in the old version of MineBot but it was so
    /// long it was easier as a Google Doc (because I could insert charts).
    /// </summary>
    protected static readonly double[] Coefficients = { 1.5, 2, 2.5, 3, 4, 5, 10 };

    /// <summary>
    /// If a path goes less than 5 blocks and doesn't make it to its goal, it's not worth considering.
    /// </summary>
    protected static readonly double MinDistPath = 5;

    /// <summary>
    /// There are floating point errors caused by random combinations of traverse and diagonal over a flat area
    /// that means that sometimes there's a cost improvement of like 10 ^ -16
    /// it's not worth the time to update the costs, decrease-key the heap, potentially repropagate, etc
    /// who cares about a hundredth of a tick? that's half a millisecond for crying out loud!
    /// </summary>
    protected static readonly double MinImprovement = 0.01;

    protected AbstractNodeCostSearch(BetterBlockPos realStart, int startX, int startY, int startZ, Goal goal, CalculationContext context)
    {
        RealStart = realStart;
        StartX = startX;
        StartY = startY;
        StartZ = startZ;
        Goal = goal;
        Context = context;
        Map = new Dictionary<long, PathNode>((int)Core.Baritone.Settings().PathingMapDefaultSize.Value, 
            new DictionaryComparer());
    }

    public void Cancel()
    {
        CancelRequested = true;
    }

    public PathCalculationResult Calculate(long primaryTimeout, long failureTimeout)
    {
        lock (this)
        {
            if (_isFinished)
            {
                throw new InvalidOperationException("Path finder cannot be reused!");
            }
            CancelRequested = false;
            try
            {
                var pathOpt = Calculate0(primaryTimeout, failureTimeout);
                IPath? path = pathOpt?.PostProcess();
                if (CancelRequested)
                {
                    return new PathCalculationResult(PathCalculationResult.PathCalculationResultType.Cancellation);
                }
                if (path == null)
                {
                    return new PathCalculationResult(PathCalculationResult.PathCalculationResultType.Failure);
                }
                int previousLength = path.Length();
                path = path.CutoffAtLoadedChunks(Context.Bsi);
                if (path.Length() < previousLength)
                {
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java:118
                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                    {
                        Context.GetBaritone().GetGameEventHandler().LogDirect("Cutting off path at edge of loaded chunks");
                        Context.GetBaritone().GetGameEventHandler().LogDirect($"Length decreased by {previousLength - path.Length()}");
                    }
                }
                else
                {
                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                    {
                        Context.GetBaritone().GetGameEventHandler().LogDirect("Path ends within loaded chunks");
                    }
                }
                previousLength = path.Length();
                path = path.StaticCutoff(Goal);
                if (path.Length() < previousLength)
                {
                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                    {
                        Context.GetBaritone().GetGameEventHandler().LogDirect($"Static cutoff {previousLength} to {path.Length()}");
                    }
                }
                if (Goal.IsInGoal(path.GetDest()))
                {
                    return new PathCalculationResult(PathCalculationResult.PathCalculationResultType.SuccessToGoal, path);
                }
                else
                {
                    return new PathCalculationResult(PathCalculationResult.PathCalculationResultType.SuccessSegment, path);
                }
            }
            catch (Exception e)
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java:142
                Context.GetBaritone().GetGameEventHandler().LogDirect($"Pathing exception: {e}");
                Console.WriteLine($"Pathing exception: {e}");
                return new PathCalculationResult(PathCalculationResult.PathCalculationResultType.Exception);
            }
            finally
            {
                _isFinished = true;
            }
        }
    }

    protected abstract IPath? Calculate0(long primaryTimeout, long failureTimeout);

    /// <summary>
    /// Determines the distance squared from the specified node to the start node.
    /// Intended for use in distance comparison, rather than anything that considers the real distance value, hence the "sq".
    /// </summary>
    protected double GetDistFromStartSq(PathNode n)
    {
        int xDiff = n.X - StartX;
        int yDiff = n.Y - StartY;
        int zDiff = n.Z - StartZ;
        return xDiff * xDiff + yDiff * yDiff + zDiff * zDiff;
    }

    /// <summary>
    /// Attempts to search the block position hashCode long to PathNode map
    /// for the node mapped to the specified pos. If no node is found,
    /// a new node is created.
    /// </summary>
    protected PathNode GetNodeAtPosition(int x, int y, int z, long hashCode)
    {
        if (!Map.TryGetValue(hashCode, out var node))
        {
            node = new PathNode(x, y, z, Goal);
            Map[hashCode] = node;
        }
        return node;
    }

    public IPath? PathToMostRecentNodeConsidered()
    {
        if (MostRecentConsidered == null || StartNode == null)
        {
            return null;
        }
        return new Path(RealStart, StartNode, MostRecentConsidered, 0, Goal, Context);
    }

    public IPath? BestPathSoFar()
    {
        return BestSoFarInternal(false, 0);
    }

    protected IPath? BestSoFarInternal(bool logInfo, int numNodes)
    {
        if (StartNode == null)
        {
            return null;
        }
        double bestDist = 0;
        for (int i = 0; i < Coefficients.Length; i++)
        {
            if (BestSoFar[i] == null)
            {
                continue;
            }
            double dist = GetDistFromStartSq(BestSoFar[i]!); // Null check above ensures non-null
            if (dist > bestDist)
            {
                bestDist = dist;
            }
            if (dist > MinDistPath * MinDistPath) // square the comparison since distFromStartSq is squared
            {
                if (logInfo)
                {
                    if (Coefficients[i] >= 3)
                    {
                        Console.WriteLine("Warning: cost coefficient is greater than three! Probably means that");
                        Console.WriteLine("the path I found is pretty terrible (like sneak-bridging for dozens of blocks)");
                        Console.WriteLine("But I'm going to do it anyway, because yolo");
                    }
                    Console.WriteLine($"Path goes for {Math.Sqrt(dist)} blocks");
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java:225
                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                    {
                        Context.GetBaritone().GetGameEventHandler().LogDirect($"A* cost coefficient {Coefficients[i]}");
                    }
                }
                return new Path(RealStart, StartNode, BestSoFar[i]!, numNodes, Goal, Context);
            }
        }
        // instead of returning bestSoFar[0], be less misleading
        if (logInfo)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java:233
            if (Core.Baritone.Settings().DebugPathCompletion.Value)
            {
                Context.GetBaritone().GetGameEventHandler().LogDirect($"Even with a cost coefficient of {Coefficients[Coefficients.Length - 1]}, I couldn't get more than {Math.Sqrt(bestDist)} blocks");
                Context.GetBaritone().GetGameEventHandler().LogDirect("No path found =(");
            }
            Context.GetBaritone().GetGameEventHandler().LogNotification("No path found =(", true);
            Console.WriteLine($"Even with a cost coefficient of {Coefficients[Coefficients.Length - 1]}, I couldn't get more than {Math.Sqrt(bestDist)} blocks");
            Console.WriteLine("No path found =(");
        }
        return null;
    }

    public bool IsFinished() => _isFinished;

    public Goal GetGoal() => Goal;

    public BetterBlockPos GetStart()
    {
        return new BetterBlockPos(StartX, StartY, StartZ);
    }

    protected int MapSize() => Map.Count;

    /// <summary>
    /// Custom comparer for Dictionary to use load factor.
    /// </summary>
    private class DictionaryComparer : IEqualityComparer<long>
    {
        public bool Equals(long x, long y) => x == y;
        public int GetHashCode(long obj) => obj.GetHashCode();
    }
}

