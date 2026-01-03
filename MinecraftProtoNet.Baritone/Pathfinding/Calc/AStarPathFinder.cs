using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Pathfinding.Goals;
using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// A* pathfinding algorithm implementation.
/// Based on Baritone's AStarPathFinder.java.
/// </summary>
public class AStarPathFinder(CalculationContext context, IGoal goal, int startX, int startY, int startZ, Favoring? favoring = null) : IPathFinder
{
    private readonly Dictionary<long, PathNode> _nodeMap = new();
    private readonly BinaryHeapOpenSet _openSet = new();
    private readonly Favoring? _favoring = favoring;
    private readonly bool _isFavoring;

    private volatile bool _cancelRequested;
    private readonly Lock _bestPathLock = new();

    // Coefficients for tracking best partial paths
    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java:69
    private static readonly double[] Coefficients = [1.5, 2.0, 2.5, 3.0, 4.0, 5.0, 10.0];
    private readonly PathNode?[] _bestByCoefficient = new PathNode?[Coefficients.Length];
    private readonly double[] _bestHeuristicByCoefficient = new double[Coefficients.Length];

    /// <summary>
    /// Minimum distance from start required for a valid partial path.
    /// </summary>
    private const double MinDistPath = 5.0;

    /// <summary>
    /// Minimum improvement required to repropagate costs (avoid floating point noise).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java:83
    /// </summary>
    private const double MinImprovement = 0.01;

    /// <summary>
    /// Time check interval (check timeout every N nodes).
    /// </summary>
    private const int TimeCheckInterval = 64;

    /// <summary>
    /// Requests cancellation of the current calculation.
    /// </summary>
    public void Cancel()
    {
        _cancelRequested = true;
    }

    /// <summary>
    /// Gets the best path found so far during calculation (for pause logic).
    /// Returns null if no valid partial path exists yet.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java:186-188
    /// </summary>
    public Path? BestPathSoFar()
    {
        lock (_bestPathLock)
        {
            var bestNode = GetBestPartialPath();
            if (bestNode == null) return null;

            // Reconstruct path using 0 for numNodes (Java passes 0 in bestPathSoFar())
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AbstractNodeCostSearch.java:187
            return ReconstructPath(bestNode, goal, 0);
        }
    }

    /// <summary>
    /// Calculates a path with the given timeout.
    /// </summary>
    /// <param name="primaryTimeoutMs">Timeout for full path calculation</param>
    /// <param name="failureTimeoutMs">Extended timeout when no path found yet</param>
    /// <returns>Path calculation result</returns>
    public (PathCalculationResultType Type, Path? Path) Calculate(long primaryTimeoutMs, long failureTimeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        var primaryTimeout = primaryTimeoutMs;
        var failureTimeout = failureTimeoutMs;

        // Initialize start node
        var startNode = GetOrCreateNode(startX, startY, startZ);
        startNode.Cost = 0;
        startNode.EstimatedCostToGoal = goal.Heuristic(startX, startY, startZ);
        startNode.CombinedCost = startNode.EstimatedCostToGoal;

        context.Logger?.LogDebug(
            "[Pathfinder] Starting A* from ({StartX}, {StartY}, {StartZ}) to goal with heuristic={StartNodeEstimatedCostToGoal}", startX,
            startY, startZ, startNode.EstimatedCostToGoal);
        context.Logger?.LogDebug(
            "[Pathfinder] Costs: Place={ContextPlaceBlockCost}, Jump={ContextJumpPenalty}, Walk={WalkOneBlockCost}, Jump1={JumpOneBlockCost}",
            context.PlaceBlockCost, context.JumpPenalty, ActionCosts.WalkOneBlockCost, ActionCosts.JumpOneBlockCost);

        _openSet.Insert(startNode);

        // Initialize best tracking
        lock (_bestPathLock)
        {
            for (var i = 0; i < Coefficients.Length; i++)
            {
                _bestHeuristicByCoefficient[i] = startNode.EstimatedCostToGoal;
                _bestByCoefficient[i] = startNode;
            }
        }

        var numNodes = 0;
        var numMovementsConsidered = 0;
        var numEmptyChunk = 0;
        var failing = true; // Haven't found a good partial path yet
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:80
        // pathingMaxChunkBorderFetch limits how many unloaded chunks we'll try to path into
        const int pathingMaxChunkBorderFetch = 50; // Default Baritone setting

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:83
        // Loop termination: openSet not empty AND numEmptyChunk < limit AND not cancelled
        while (!_openSet.IsEmpty && numEmptyChunk < pathingMaxChunkBorderFetch && !_cancelRequested)
        {
            if (_cancelRequested) return (PathCalculationResultType.Cancelled, null);

            // Timeout check
            if ((numNodes & (TimeCheckInterval - 1)) == 0)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                var bestNode = GetBestPartialPath(); // Re-evaluate best partial path for timeout checks

                if (elapsed >= failureTimeout && bestNode == null)
                {
                    context.Logger?.LogWarning("[AStar] Timeout (Failure).");
                    return (PathCalculationResultType.Failure, null);
                }

                if (elapsed >= primaryTimeout)
                {
                    if (bestNode != null)
                    {
                        context.Logger?.LogWarning("[AStar] Timeout (BestEffort). Reconstructing.");
                        return (PathCalculationResultType.PartialSuccess, ReconstructPath(bestNode, goal, numNodes));
                    }

                    context.Logger?.LogWarning("[AStar] Timeout (No Best Node).");
                    return (PathCalculationResultType.Failure, null);
                }
            }

            var currentNode = _openSet.RemoveLowest();
            numNodes++;

            // Log node selection pattern to understand exploration (focus on Y coordinate to see vertical exploration)
            if (numNodes <= 10 || (numNodes % 20 == 0))
            {
                var distFromStart = Math.Sqrt(GetDistFromStartSq(currentNode));
                var yDiff = currentNode.Y - startY;
                context.Logger?.LogDebug(
                    "[AStar] Exploring node {NumNodes}: ({X}, {Y}, {Z}), YDiff={YDiff}, Cost={Cost:F2}, Heuristic={Heuristic:F2}, Combined={Combined:F2}, DistFromStart={DistStart:F2}",
                    numNodes, currentNode.X, currentNode.Y, currentNode.Z, yDiff, currentNode.Cost, currentNode.EstimatedCostToGoal,
                    currentNode.CombinedCost, distFromStart);
            }

            // Baritone AStarPathFinder.java line 98-100: Check if we've reached the goal
            if (goal.IsInGoal(currentNode.X, currentNode.Y, currentNode.Z))
            {
                var successPath = ReconstructPath(currentNode, goal, numNodes);
                context.Logger?.LogDebug(
                    "[AStar] Goal reached! Path has {PositionsCount} positions: {Join}", successPath.Positions.Count, string.Join(" -> ",
                        successPath.Positions));
                return (PathCalculationResultType.Success, successPath);
            }

            // Expand neighbors
            int movementsGenerated = 0;
            int movementsRejectedBounds = 0;
            int movementsRejectedChunk = 0;
            int movementsRejectedCostInf = 0;
            int movementsRejectedMinImprovement = 0;
            int movementsAdded = 0;
            int pillarMovements = 0;
            int pillarRejected = 0;

            foreach (var (dx, dy, dz, cost) in GetPossibleMoves(currentNode))
            {
                movementsGenerated++;
                var newX = currentNode.X + dx;
                var newY = currentNode.Y + dy;
                var newZ = currentNode.Z + dz;

                // Track pillar movements specifically
                if (dx == 0 && dy == 1 && dz == 0)
                {
                    pillarMovements++;
                    if (cost >= ActionCosts.CostInf)
                    {
                        pillarRejected++;
                        if (numNodes <= 5) // Log first few pillar rejections to debug
                        {
                            context.Logger?.LogDebug("[AStar] Pillar movement rejected at ({X}, {Y}, {Z}): Cost={Cost}", currentNode.X,
                                currentNode.Y, currentNode.Z, cost);
                        }
                    }
                }

                // Bounds check
                if (newY < context.MinY || newY > context.MaxY)
                {
                    movementsRejectedBounds++;
                    continue;
                }

                // Chunk load check
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:105-110
                if ((newX >> 4 != currentNode.X >> 4 || newZ >> 4 != currentNode.Z >> 4) && !context.IsLoaded(newX, newZ))
                {
                    // Only increment counter if the movement would have gone out of bounds guaranteed
                    // (i.e., not a dynamic movement that could modify destination)
                    // Note: Our GetPossibleMoves returns fixed offsets, so we increment here
                    numEmptyChunk++;
                    movementsRejectedChunk++;
                    continue;
                }

                // World border check for fixed movements
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:112-114
                if (context.WorldBorder != null && !context.WorldBorder.EntirelyContains(newX, newZ))
                {
                    movementsRejectedBounds++;
                    continue;
                }

                numMovementsConsidered++;
                if (cost >= ActionCosts.CostInf)
                {
                    movementsRejectedCostInf++;
                    continue;
                }

                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:125-133
                // Validate cost is not NaN or <= 0
                if (double.IsNaN(cost) || cost <= 0)
                {
                    context.Logger?.LogError(
                        "[AStar] Movement from ({X}, {Y}, {Z}) calculated implausible cost {Cost}",
                        currentNode.X, currentNode.Y, currentNode.Z, cost);
                    movementsRejectedCostInf++;
                    continue;
                }

                // World border check for dynamic movements (after cost calculation, destination may have been modified)
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:135-137
                // Note: In our implementation, GetPossibleMoves already returns final destinations for dynamic movements,
                // so we check world border here for all movements (both fixed and dynamic)
                // The check above handles fixed movements, this is a redundant check for safety
                // Actually, dynamic movements already have their final destination in newX, newZ, so the check above is sufficient

                var actionCost = cost;

                // Apply favoring multiplier
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:161-164
                if (_isFavoring)
                {
                    var hashCode = PathNode.CalculateHash(newX, newY, newZ);
                    actionCost *= _favoring!.Calculate(hashCode);
                }

                var neighbor = GetOrCreateNode(newX, newY, newZ);
                var tentativeCost = currentNode.Cost + actionCost;

                // Baritone AStarPathFinder.java line 167: Use minimumImprovement threshold (default 0.01 to avoid floating point noise)
                // Comparison: neighbor.cost - tentativeCost > minimumImprovement
                // Equivalent to: tentativeCost < neighbor.Cost - minimumImprovement
                if (neighbor.Cost - tentativeCost > MinImprovement)
                {
                    neighbor.Previous = currentNode;
                    neighbor.Cost = tentativeCost;
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:170
                    // Use cached estimatedCostToGoal (calculated in GetOrCreateNode), don't recalculate
                    neighbor.CombinedCost = tentativeCost + neighbor.EstimatedCostToGoal;

                    // Baritone AStarPathFinder.java lines 171-175: Update if already in open set, insert otherwise
                    if (neighbor.IsOpen())
                    {
                        _openSet.Update(neighbor);
                    }
                    else
                    {
                        _openSet.Insert(neighbor);
                        movementsAdded++;
                    }

                    // Track best partial paths
                    UpdateBestSoFar(neighbor, ref failing);
                }
                else
                {
                    movementsRejectedMinImprovement++;
                }
            }

            // Log detailed statistics every 20 nodes to understand exploration pattern
            if ((numNodes % 20 == 0) && numNodes > 0)
            {
                context.Logger?.LogDebug(
                    "[AStar] Node {NumNodes}: Generated={Generated}, Rejected(Bounds={Bounds}, Chunk={Chunk}, CostInf={CostInf}, MinImprove={MinImprove}), Added={Added}, OpenSet={OpenSet}, Pillar(Tried={PillarTried}, Rejected={PillarRejected})",
                    numNodes, movementsGenerated, movementsRejectedBounds, movementsRejectedChunk, movementsRejectedCostInf,
                    movementsRejectedMinImprovement, movementsAdded, _openSet.Count, pillarMovements, pillarRejected);
            }
        }

        context.Logger?.LogDebug(
            "[AStar] Loop finished. Nodes={NumNodes}, Movements={Movements}, OpenSetEmpty={OpenSetEmpty}, Canceled={Canceled}, OpenSetCount={OpenSetCount}, EmptyChunks={EmptyChunks}",
            numNodes, numMovementsConsidered, _openSet.IsEmpty, _cancelRequested, _openSet.Count, numEmptyChunk);

        if (_cancelRequested)
        {
            context.Logger?.LogDebug("[AStar] Calculation cancelled after {NumNodes} nodes", numNodes);
            return (PathCalculationResultType.Cancelled, null);
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:192-195
        // Log statistics when loop terminates
        context.Logger?.LogDebug("[AStar] {Movements} movements considered", numMovementsConsidered);
        context.Logger?.LogDebug("[AStar] Open set size: {Size}", _openSet.Count);
        context.Logger?.LogDebug("[AStar] PathNode map size: {Size}", _nodeMap.Count);

        // Return best partial path if we have one
        var bestPartial = GetBestPartialPath();
        if (bestPartial != null)
        {
            var path = ReconstructPath(bestPartial, goal, numNodes);
            context.Logger?.LogDebug("[AStar] Partial Result: {Count} nodes", path.Positions.Count);
            return (PathCalculationResultType.PartialSuccess, path);
        }

        context.Logger?.LogWarning("[AStar] Calculation failed after {NumNodes} nodes. No path or partial path found.", numNodes);
        return (PathCalculationResultType.Failure, null);
    }

    private Path ReconstructPath(PathNode endNode, IGoal goal, int numNodesConsidered)
    {
        context.Logger?.LogDebug("[AStar] ReconstructPath called.");
        var pathNodes = new List<PathNode>();
        var current = endNode;

        while (current != null)
        {
            pathNodes.Add(current);
            current = current.Previous;
        }

        pathNodes.Reverse();

        context.Logger?.LogDebug("[AStar] Path Found: {Count} nodes.", pathNodes.Count);
        foreach (var node in pathNodes)
        {
            context.Logger?.LogTrace("[AStar] Path Node: {X}, {Y}, {Z} (Cost={Cost})", node.X, node.Y, node.Z, node.Cost);
        }

        var positions = pathNodes.Select(n => (n.X, n.Y, n.Z)).ToList();
        var reachesGoal = goal.IsInGoal(endNode.X, endNode.Y, endNode.Z);
        return new Path(positions, goal, numNodesConsidered, reachesGoal);
    }

    private void UpdateBestSoFar(PathNode node, ref bool failing)
    {
        lock (_bestPathLock)
        {
            for (var i = 0; i < Coefficients.Length; i++)
            {
                var heuristic = node.EstimatedCostToGoal + node.Cost / Coefficients[i];
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:178
                // Use minimumImprovement threshold to avoid floating point noise
                if (_bestHeuristicByCoefficient[i] - heuristic > MinImprovement)
                {
                    _bestHeuristicByCoefficient[i] = heuristic;
                    _bestByCoefficient[i] = node;

                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:181-183
                    if (failing && GetDistFromStartSq(node) > MinDistPath * MinDistPath)
                    {
                        failing = false;
                    }
                }
            }
        }
    }

    private double GetDistFromStartSq(PathNode node)
    {
        var dx = node.X - startX;
        var dy = node.Y - startY;
        var dz = node.Z - startZ;
        return dx * dx + dy * dy + dz * dz;
    }

    private PathNode? GetBestPartialPath()
    {
        // Baritone AbstractNodeCostSearch.bestSoFar() lines 190-224
        // Find the best partial path based on distance from start, NOT closeness to goal
        // This prevents selecting dead-end paths just because they're geometrically closer to goal
        double bestDist = 0;

        for (int i = 0; i < Coefficients.Length; i++)
        {
            var node = _bestByCoefficient[i];
            if (node == null) continue;

            var dist = GetDistFromStartSq(node);
            if (dist > bestDist)
            {
                bestDist = dist;
            }

            // Baritone line 203: Only consider paths that travel minimum distance from start
            if (dist > MinDistPath * MinDistPath)
            {
                if (Coefficients[i] >= 3)
                {
                    context.Logger?.LogWarning("[AStar] Cost coefficient is {Coef}! Path may be suboptimal.", Coefficients[i]);
                }

                return node; // Return first valid path meeting distance threshold
            }
        }

        // Baritone lines 216-223: Don't return a path that barely left the start
        context.Logger?.LogDebug(
            "[AStar] No partial path met the minimum distance requirement ({MinDist} blocks). Best distance reached: {BestDist} blocks",
            MinDistPath, Math.Sqrt(bestDist));
        return null;
    }

    private PathNode GetOrCreateNode(int x, int y, int z)
    {
        var hash = PathNode.CalculateHash(x, y, z);
        if (!_nodeMap.TryGetValue(hash, out var node))
        {
            node = new PathNode(x, y, z);
            node.EstimatedCostToGoal = goal.Heuristic(x, y, z);
            _nodeMap[hash] = node;
        }

        return node;
    }

    /// <summary>
    /// Gets possible moves from the current position.
    /// </summary>
    private IEnumerable<(int dx, int dy, int dz, double cost)> GetPossibleMoves(PathNode current)
    {
        // Cardinal directions (traverse)
        foreach (var move in GetCardinalMoves(current)) yield return move;

        // Diagonal directions (includes Ascend/Descend)
        foreach (var move in GetDiagonalMoves(current)) yield return move;

        // Pillar (straight up)
        yield return (0, 1, 0, GetPillarCost(current.X, current.Y, current.Z));

        // Downward (straight down)
        yield return (0, -1, 0, GetDownwardCost(current.X, current.Y, current.Z));

        // Parkour (gap jumps)
        foreach (var move in GetParkourMoves(current)) yield return move;

        // Multi-block falls (for descending into caves, off edges, etc.)
        foreach (var move in GetFallMoves(current)) yield return move;
    }

    private IEnumerable<(int dx, int dy, int dz, double cost)> GetCardinalMoves(PathNode current)
    {
        // Traverse movements (fixed destinations)
        yield return (1, 0, 0, GetTraverseCost(current.X, current.Y, current.Z, 1, 0));
        yield return (-1, 0, 0, GetTraverseCost(current.X, current.Y, current.Z, -1, 0));
        yield return (0, 0, 1, GetTraverseCost(current.X, current.Y, current.Z, 0, 1));
        yield return (0, 0, -1, GetTraverseCost(current.X, current.Y, current.Z, 0, -1));

        // Ascend movements (fixed destinations)
        yield return (1, 1, 0, GetAscendCost(current.X, current.Y, current.Z, 1, 0));
        yield return (-1, 1, 0, GetAscendCost(current.X, current.Y, current.Z, -1, 0));
        yield return (0, 1, 1, GetAscendCost(current.X, current.Y, current.Z, 0, 1));
        yield return (0, 1, -1, GetAscendCost(current.X, current.Y, current.Z, 0, -1));

        // Descend movements (dynamic destinations via MutableMoveResult)
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Moves.java:151-221
        var descendResult = new MutableMoveResult();
        GetDescendCost(current.X, current.Y, current.Z, current.X + 1, current.Z, descendResult);
        if (descendResult.Cost < ActionCosts.CostInf)
        {
            yield return (descendResult.X - current.X, descendResult.Y - current.Y, descendResult.Z - current.Z, descendResult.Cost);
        }

        descendResult.Reset();
        GetDescendCost(current.X, current.Y, current.Z, current.X - 1, current.Z, descendResult);
        if (descendResult.Cost < ActionCosts.CostInf)
        {
            yield return (descendResult.X - current.X, descendResult.Y - current.Y, descendResult.Z - current.Z, descendResult.Cost);
        }

        descendResult.Reset();
        GetDescendCost(current.X, current.Y, current.Z, current.X, current.Z + 1, descendResult);
        if (descendResult.Cost < ActionCosts.CostInf)
        {
            yield return (descendResult.X - current.X, descendResult.Y - current.Y, descendResult.Z - current.Z, descendResult.Cost);
        }

        descendResult.Reset();
        GetDescendCost(current.X, current.Y, current.Z, current.X, current.Z - 1, descendResult);
        if (descendResult.Cost < ActionCosts.CostInf)
        {
            yield return (descendResult.X - current.X, descendResult.Y - current.Y, descendResult.Z - current.Z, descendResult.Cost);
        }
    }

    private IEnumerable<(int dx, int dy, int dz, double cost)> GetDiagonalMoves(PathNode current)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Moves.java:223-277
        // Diagonal movements use MutableMoveResult for dynamic Y coordinate
        int[] dxs = { 1, 1, -1, -1 };
        int[] dzs = { 1, -1, 1, -1 };

        for (int i = 0; i < 4; i++)
        {
            var dx = dxs[i];
            var dz = dzs[i];
            var destX = current.X + dx;
            var destZ = current.Z + dz;

            // Diagonal movement (can modify Y coordinate dynamically)
            var diagonalResult = new MutableMoveResult();
            GetDiagonalCost(current.X, current.Y, current.Z, destX, destZ, diagonalResult);
            if (diagonalResult.Cost < ActionCosts.CostInf)
            {
                yield return (diagonalResult.X - current.X, diagonalResult.Y - current.Y, diagonalResult.Z - current.Z, diagonalResult.Cost);
            }
        }
    }

    private IEnumerable<(int dx, int dy, int dz, double cost)> GetParkourMoves(PathNode current)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Moves.java:279-325
        // Parkour movements use MutableMoveResult for dynamic destinations
        var directions = new[]
        {
            MoveDirection.ParkourNorth,
            MoveDirection.ParkourSouth,
            MoveDirection.ParkourEast,
            MoveDirection.ParkourWest
        };

        foreach (var dir in directions)
        {
            var parkourResult = new MutableMoveResult();
            GetParkourCost(current.X, current.Y, current.Z, dir, parkourResult);
            if (parkourResult.Cost < ActionCosts.CostInf)
            {
                yield return (parkourResult.X - current.X, parkourResult.Y - current.Y, parkourResult.Z - current.Z, parkourResult.Cost);
            }
        }
    }

    /// <summary>
    /// Calculates the cost of a parkour movement, potentially modifying the destination coordinates.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java:63-201
    /// </summary>
    private void GetParkourCost(int x, int y, int z, MoveDirection dir, MutableMoveResult res)
    {
        if (!context.AllowParkour)
        {
            return;
        }
        // Note: allowJumpAtBuildLimit check not implemented (would need context.MaxY check)
        
        int xDiff = dir.XOffset / Math.Max(1, Math.Abs(dir.XOffset)); // Normalize to -1, 0, or 1
        int zDiff = dir.ZOffset / Math.Max(1, Math.Abs(dir.ZOffset));
        
        if (xDiff == 0 && zDiff == 0)
        {
            return;
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java:72-75
        // Check adjacent block is passable
        var adjBlock = context.GetBlockState(x + xDiff, y, z + zDiff);
        if (!MovementHelper.FullyPassable(adjBlock))
        {
            return;
        }
        
        var adj = context.GetBlockState(x + xDiff, y - 1, z + zDiff);
        // Reference: baritone line 77: don't parkour if we could just traverse
        if (MovementHelper.CanWalkOn(adj))
        {
            return;
        }
        
        // Reference: baritone line 81: avoid walking into magma (unless water)
        if (MovementHelper.AvoidWalkingInto(adj) && !MovementHelper.IsWater(adj))
        {
            return;
        }
        
        // Reference: baritone lines 84-92: Check clearance for jump
        if (!MovementHelper.FullyPassable(context, x + xDiff, y + 1, z + zDiff))
        {
            return;
        }
        if (!MovementHelper.FullyPassable(context, x + xDiff, y + 2, z + zDiff))
        {
            return;
        }
        if (!MovementHelper.FullyPassable(context, x, y + 2, z))
        {
            return;
        }
        
        var standingOn = context.GetBlockState(x, y - 1, z);
        // Reference: baritone line 94: Can't jump from ladder/vine/stair/bottom slab
        if (MovementHelper.IsClimbable(standingOn) || MovementHelper.IsBottomSlab(standingOn))
        {
            // Note: StairBlock check not implemented (would need block type check)
            return;
        }
        
        // Reference: baritone lines 98-102: Can't jump from water
        if (context.AssumeWalkOnWater && MovementHelper.IsLiquid(standingOn))
        {
            return;
        }
        if (MovementHelper.IsLiquid(context.GetBlockState(x, y, z)))
        {
            return;
        }
        
        // Reference: baritone lines 104-113: Determine max jump distance
        int maxJump;
        if (MovementHelper.IsSoulSand(standingOn))
        {
            maxJump = 2; // 1 block gap
        }
        else
        {
            maxJump = context.CanSprint ? 4 : 3;
        }
        
        // Reference: baritone lines 115-164: Check parkour jumps from smallest to largest
        int verifiedMaxJump = 1;
        for (int i = 2; i <= maxJump; i++)
        {
            int destX = x + xDiff * i;
            int destZ = z + zDiff * i;
            
            // Check head/feet clearance
            if (!MovementHelper.FullyPassable(context, destX, y + 1, destZ))
            {
                break;
            }
            if (!MovementHelper.FullyPassable(context, destX, y + 2, destZ))
            {
                break;
            }
            
            // Reference: baritone lines 129-139: Check for ascend landing position
            var destInto = context.GetBlockState(destX, y, destZ);
            if (!MovementHelper.FullyPassable(destInto))
            {
                if (i <= 3 && context.AllowParkourAscend && context.CanSprint && MovementHelper.CanWalkOn(destInto) && CheckOvershootSafety(destX + xDiff, y + 1, destZ + zDiff))
                {
                    res.X = destX;
                    res.Y = y + 1;
                    res.Z = destZ;
                    res.Cost = i * ActionCosts.SprintOneBlockCost + context.JumpPenalty;
                    return;
                }
                break;
            }
            
            // Reference: baritone lines 142-157: Check for flat landing position
            var landingOn = context.GetBlockState(destX, y - 1, destZ);
            bool canLand = (landingOn?.Name.Contains("farmland", StringComparison.OrdinalIgnoreCase) != true && MovementHelper.CanWalkOn(landingOn))
                          || (Math.Min(16, context.FrostWalker + 2) >= i && MovementHelper.CanUseFrostWalker(context, landingOn));
            
            if (canLand)
            {
                if (CheckOvershootSafety(destX + xDiff, y, destZ + zDiff))
                {
                    res.X = destX;
                    res.Y = y;
                    res.Z = destZ;
                    res.Cost = CostFromJumpDistance(i) + context.JumpPenalty;
                    return;
                }
                break;
            }
            
            if (!MovementHelper.FullyPassable(context, destX, y + 3, destZ))
            {
                break;
            }
            
            verifiedMaxJump = i;
        }
        
        // Reference: baritone lines 166-200: Parkour place logic
        if (!context.AllowParkourPlace)
        {
            return;
        }
        
        // Check from largest to smallest for positions to place blocks
        for (int i = verifiedMaxJump; i > 1; i--)
        {
            int destX = x + i * xDiff;
            int destZ = z + i * zDiff;
            var toReplace = context.GetBlockState(destX, y - 1, destZ);
            double placeCost = context.CostOfPlacingAt(destX, y - 1, destZ);
            if (placeCost >= ActionCosts.CostInf)
            {
                continue;
            }
            if (!MovementHelper.IsReplaceable(toReplace))
            {
                continue;
            }
            if (!CheckOvershootSafety(destX + xDiff, y, destZ + zDiff))
            {
                continue;
            }
            
            // Reference: baritone lines 185-199: Check for placement against adjacent blocks
            // HORIZONTALS_BUT_ALSO_DOWN: North, South, East, West, Down
            (int X, int Y, int Z)[] directions = [
                (0, 0, -1), (0, 0, 1), (1, 0, 0), (-1, 0, 0), (0, -1, 0)
            ];
            for (int j = 0; j < directions.Length; j++)
            {
                int againstX = destX + directions[j].X;
                int againstY = y - 1 + directions[j].Y;
                int againstZ = destZ + directions[j].Z;
                if (againstX == destX - xDiff && againstZ == destZ - zDiff)
                {
                    continue; // can't turn around that fast
                }
                if (MovementHelper.CanPlaceAgainst(context.GetBlockState(againstX, againstY, againstZ)))
                {
                    res.X = destX;
                    res.Y = y;
                    res.Z = destZ;
                    res.Cost = CostFromJumpDistance(i) + placeCost + context.JumpPenalty;
                    return;
                }
            }
        }
    }
    
    /// <summary>
    /// Checks if we can safely overshoot the landing position (won't hit avoidWalkingInto blocks).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java:203-206
    /// </summary>
    private bool CheckOvershootSafety(int x, int y, int z)
    {
        var block1 = context.GetBlockState(x, y, z);
        var block2 = context.GetBlockState(x, y + 1, z);
        return !MovementHelper.AvoidWalkingInto(block1) && !MovementHelper.AvoidWalkingInto(block2);
    }
    
    /// <summary>
    /// Calculates cost based on jump distance.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java:208-219
    /// </summary>
    private static double CostFromJumpDistance(int dist)
    {
        return dist switch
        {
            2 => ActionCosts.WalkOneBlockCost * 2,
            3 => ActionCosts.WalkOneBlockCost * 3,
            4 => ActionCosts.SprintOneBlockCost * 4,
            _ => throw new ArgumentException($"Invalid jump distance: {dist}")
        };
    }

    /// <summary>
    /// Generates multi-block fall moves by scanning downward in cardinal directions.
    /// Parity with Baritone's MovementDescend.dynamicFallCost logic.
    /// </summary>
    private IEnumerable<(int dx, int dy, int dz, double cost)> GetFallMoves(PathNode current)
    {
        int x = current.X;
        int y = current.Y;
        int z = current.Z;

        // Check if we can fall straight down (no horizontal movement)
        foreach (var fall in TryFallDown(x, y, z, 0, 0))
        {
            yield return fall;
        }

        // Check cardinal directions for edge falls
        int[] dxs = { 1, -1, 0, 0 };
        int[] dzs = { 0, 0, 1, -1 };

        for (int i = 0; i < 4; i++)
        {
            int dx = dxs[i];
            int dz = dzs[i];

            foreach (var fall in TryFallDown(x, y, z, dx, dz))
            {
                yield return fall;
            }
        }
    }

    /// <summary>
    /// Attempts to find a valid fall landing zone at the given horizontal offset.
    /// </summary>
    private IEnumerable<(int dx, int dy, int dz, double cost)> TryFallDown(int x, int y, int z, int dx, int dz)
    {
        int destX = x + dx;
        int destZ = z + dz;

        // For horizontal falls, need to be able to walk off the edge first
        if (dx != 0 || dz != 0)
        {
            var edgeBody = context.GetBlockState(destX, y, destZ);
            var edgeHead = context.GetBlockState(destX, y + 1, destZ);

            // If edge is blocked, can't fall this way
            if (!MovementHelper.CanWalkThrough(edgeBody) || !MovementHelper.CanWalkThrough(edgeHead))
            {
                yield break;
            }
        }

        // Scan downward for valid landing zone
        int maxFall = context.HasWaterBucket ? context.MaxFallHeightBucket : context.MaxFallHeightNoWater;

        for (int fallHeight = 2; fallHeight <= maxFall + 1; fallHeight++)
        {
            int newY = y - fallHeight;

            // Don't scan below world
            if (newY < -64) break;

            var ontoBlock = context.GetBlockState(destX, newY, destZ);

            // Check if we can walk through this block (keep falling)
            if (MovementHelper.CanWalkThrough(ontoBlock))
            {
                continue;
            }

            // Check if this is water (safe landing)
            if (MovementHelper.IsWater(ontoBlock))
            {
                // Found water landing
                var cost = ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(fallHeight);
                yield return (dx, -fallHeight + 1, dz, cost); // +1 because we land IN the water
                yield break;
            }

            // Check if we can walk on this block (valid landing)
            if (!MovementHelper.CanWalkOn(ontoBlock))
            {
                yield break; // Can't land here and can't fall through - stuck
            }

            // Baritone avoids bottom slabs (glitchy fall damage)
            if (MovementHelper.IsBottomSlab(ontoBlock))
            {
                yield break;
            }

            // Found valid landing - land on TOP of this block
            // destY = newY + 1 (we stand on the block)
            int landingY = newY + 1;
            int dy = landingY - y;

            var fallCost = ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(fallHeight - 1) + ActionCosts.CenterAfterFallCost;
            yield return (dx, dy, dz, fallCost);
            yield break;
        }
    }

    private double GetTraverseCost(int x, int y, int z, int dx, int dz)
    {
        var destX = x + dx;
        var destZ = z + dz;

        var destFloor = context.GetBlockState(destX, y - 1, destZ);
        double placeCost = 0;
        if (!MovementHelper.CanWalkOn(destFloor))
        {
            if (!context.AllowPlace || !context.HasThrowaway || !MovementHelper.IsReplaceable(destFloor))
            {
                return ActionCosts.CostInf;
            }

            // Baritone MovementTraverse.cost lines 142-152: Check for side-place options
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementTraverse.java:142-152
            bool foundPlaceOption = false;
            int[][] directions = [[0, 0, -1], [0, 0, 1], [1, 0, 0], [-1, 0, 0], [0, -1, 0]]; // HORIZONTALS_BUT_ALSO_DOWN
            for (int i = 0; i < 5; i++)
            {
                int againstX = destX + directions[i][0];
                int againstY = (y - 1) + directions[i][1];
                int againstZ = destZ + directions[i][2];
                // Skip if it's our source position (backplace case - checked separately)
                if (againstX == x && againstZ == z)
                {
                    continue;
                }

                if (MovementHelper.CanPlaceAgainst(context.GetBlockState(againstX, againstY, againstZ)))
                {
                    foundPlaceOption = true;
                    break;
                }
            }

            // Baritone lines 154-165: Backplace case - check if possible
            if (!foundPlaceOption)
            {
                var srcDown = context.GetBlockState(x, y - 1, z);
                bool standingOnABlock = MovementHelper.CanWalkOn(srcDown);

                // Can't backplace against soul sand or slabs
                if (MovementHelper.IsSoulSand(srcDown) || MovementHelper.IsBottomSlab(srcDown))
                {
                    return ActionCosts.CostInf;
                }

                // Can't backplace if not standing on a block
                if (!standingOnABlock)
                {
                    return ActionCosts.CostInf;
                }

                // Baritone lines 161-163: Lily pad/carpet check
                var blockSrc = context.GetBlockState(x, y, z);
                if ((MovementHelper.IsLilyPad(blockSrc) || MovementHelper.IsCarpet(blockSrc)) &&
                    MovementHelper.IsLiquid(srcDown))
                {
                    return ActionCosts.CostInf;
                }

                // Backplace is possible, but costs more (sneak cost)
                placeCost = context.PlaceBlockCost +
                            ActionCosts.WalkOneBlockCost * (ActionCosts.SneakOneBlockCost / ActionCosts.WalkOneBlockCost);
            }
            else
            {
                // Side place is possible
                placeCost = context.PlaceBlockCost + ActionCosts.WalkOneBlockCost;
            }
        }

        var destBody = context.GetBlockState(destX, y, destZ);
        var destHead = context.GetBlockState(destX, y + 1, destZ);

        // Baritone lines 107-125: Calculate mining duration for blocking blocks
        double hardness1 = GetMiningCost(destBody);
        if (hardness1 >= ActionCosts.CostInf) return ActionCosts.CostInf;

        double hardness2 = GetMiningCost(destHead);
        if (hardness2 >= ActionCosts.CostInf) return ActionCosts.CostInf;

        var cost = context.CanSprint ? ActionCosts.SprintOneBlockCost : ActionCosts.WalkOneBlockCost;
        if (MovementHelper.IsSoulSand(destFloor)) cost *= 2;

        // If we need to break blocks or place blocks, can't sprint
        if (hardness1 > 0 || hardness2 > 0 || placeCost > 0)
        {
            cost = ActionCosts.WalkOneBlockCost; // Can't sprint when breaking/placing
        }

        return cost + hardness1 + hardness2 + placeCost;
    }

    /// <summary>
    /// Gets the mining cost for a block. Returns 0 if walkable, CostInf if unbreakable.
    /// </summary>
    private double GetMiningCost(BlockState? block)
    {
        if (block == null || MovementHelper.CanWalkThrough(block)) return 0;
        if (!context.AllowBreak) return ActionCosts.CostInf;

        // Calculate mining duration
        float toolSpeed = context.GetBestToolSpeed?.Invoke(block) ?? 1.0f;
        var duration = ActionCosts.CalculateMiningDuration(toolSpeed, block.DestroySpeed, true);

        // Unbreakable blocks (bedrock, barriers, etc.)
        if (duration >= ActionCosts.CostInf || block.DestroySpeed < 0)
        {
            return ActionCosts.CostInf;
        }

        return duration;
    }

    /// <summary>
    /// Calculates the cost of a diagonal movement, potentially modifying the destination Y coordinate.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDiagonal.java:111-253
    /// </summary>
    private void GetDiagonalCost(int x, int y, int z, int destX, int destZ, MutableMoveResult res)
    {
        if (!MovementHelper.CanWalkThrough(context.GetBlockState(destX, y + 1, destZ)))
        {
            return;
        }
        var destInto = context.GetBlockState(destX, y, destZ);
        var fromDown = context.GetBlockState(x, y - 1, z);
        bool ascend = false;
        BlockState? destWalkOn;
        bool descend = false;
        bool frostWalker = false;
        if (!MovementHelper.CanWalkThrough(destInto))
        {
            ascend = true;
            if (!context.AllowDiagonalAscend || !MovementHelper.CanWalkThrough(context.GetBlockState(x, y + 2, z)) || !MovementHelper.CanWalkOn(destInto) || !MovementHelper.CanWalkThrough(context.GetBlockState(destX, y + 2, destZ)))
            {
                return;
            }
            destWalkOn = destInto;
        }
        else
        {
            destWalkOn = context.GetBlockState(destX, y - 1, destZ);
            bool standingOnABlock = MovementHelper.MustBeSolidToWalkOn(context, x, y - 1, z);
            frostWalker = standingOnABlock && MovementHelper.CanUseFrostWalker(context, destWalkOn);
            if (!frostWalker && !MovementHelper.CanWalkOn(destWalkOn))
            {
                descend = true;
                if (!context.AllowDiagonalDescend || !MovementHelper.CanWalkOn(context.GetBlockState(destX, y - 2, destZ)) || !MovementHelper.CanWalkThrough(destWalkOn))
                {
                    return;
                }
            }
            frostWalker &= !context.AssumeWalkOnWater; // do this after checking for descends because jesus can't prevent the water from freezing
        }
        double multiplier = ActionCosts.WalkOneBlockCost;
        // For either possible soul sand, that affects half of our walking
        if (MovementHelper.IsSoulSand(destWalkOn))
        {
            multiplier += (ActionCosts.WalkOneOverSoulSandCost - ActionCosts.WalkOneBlockCost) / 2;
        }
        else if (frostWalker)
        {
            // frostwalker lets us walk on water without the penalty
        }
        else if (MovementHelper.IsWater(destWalkOn))
        {
            multiplier += context.WalkOnWaterOnePenalty * Math.Sqrt(2);
        }
        if (MovementHelper.IsClimbable(fromDown))
        {
            return;
        }
        if (MovementHelper.IsSoulSand(fromDown))
        {
            multiplier += (ActionCosts.WalkOneOverSoulSandCost - ActionCosts.WalkOneBlockCost) / 2;
        }
        var cuttingOver1 = context.GetBlockState(x, y - 1, destZ);
        if (MovementHelper.IsMagmaBlock(cuttingOver1) || MovementHelper.IsLava(cuttingOver1))
        {
            return;
        }
        var cuttingOver2 = context.GetBlockState(destX, y - 1, z);
        if (MovementHelper.IsMagmaBlock(cuttingOver2) || MovementHelper.IsLava(cuttingOver2))
        {
            return;
        }
        bool water = false;
        var startState = context.GetBlockState(x, y, z);
        if (MovementHelper.IsWater(startState) || MovementHelper.IsWater(destInto))
        {
            if (ascend)
            {
                return;
            }
            // Ignore previous multiplier
            multiplier = context.WaterWalkSpeed;
            water = true;
        }
        var pb0 = context.GetBlockState(x, y, destZ);
        var pb2 = context.GetBlockState(destX, y, z);
        if (ascend)
        {
            bool ATop = MovementHelper.CanWalkThrough(context.GetBlockState(x, y + 2, destZ));
            bool AMid = MovementHelper.CanWalkThrough(context.GetBlockState(x, y + 1, destZ));
            bool ALow = MovementHelper.CanWalkThrough(pb0);
            bool BTop = MovementHelper.CanWalkThrough(context.GetBlockState(destX, y + 2, z));
            bool BMid = MovementHelper.CanWalkThrough(context.GetBlockState(destX, y + 1, z));
            bool BLow = MovementHelper.CanWalkThrough(pb2);
            if ((!(ATop && AMid && ALow) && !(BTop && BMid && BLow)) // no option
                    || MovementHelper.AvoidWalkingInto(pb0) // bad
                    || MovementHelper.AvoidWalkingInto(pb2) // bad
                    || (ATop && AMid && MovementHelper.CanWalkOn(pb0)) // we could just ascend
                    || (BTop && BMid && MovementHelper.CanWalkOn(pb2)) // we could just ascend
                    || (!ATop && AMid && ALow) // head bonk A
                    || (!BTop && BMid && BLow)) // head bonk B
            {
                return;
            }
            res.Cost = multiplier * Math.Sqrt(2) + ActionCosts.JumpOneBlockCost;
            res.X = destX;
            res.Z = destZ;
            res.Y = y + 1;
            return;
        }
        double optionA = MovementHelper.GetMiningDurationTicks(context, x, y, destZ, pb0, false);
        double optionB = MovementHelper.GetMiningDurationTicks(context, destX, y, z, pb2, false);
        if (optionA != 0 && optionB != 0)
        {
            return;
        }
        var pb1 = context.GetBlockState(x, y + 1, destZ);
        optionA += MovementHelper.GetMiningDurationTicks(context, x, y + 1, destZ, pb1, true);
        if (optionA != 0 && optionB != 0)
        {
            return;
        }
        var pb3 = context.GetBlockState(destX, y + 1, z);
        if (optionA == 0 && ((MovementHelper.AvoidWalkingInto(pb2) && !MovementHelper.IsWater(pb2)) || MovementHelper.AvoidWalkingInto(pb3)))
        {
            return;
        }
        optionB += MovementHelper.GetMiningDurationTicks(context, destX, y + 1, z, pb3, true);
        if (optionA != 0 && optionB != 0)
        {
            return;
        }
        if (optionB == 0 && ((MovementHelper.AvoidWalkingInto(pb0) && !MovementHelper.IsWater(pb0)) || MovementHelper.AvoidWalkingInto(pb1)))
        {
            return;
        }
        if (optionA != 0 || optionB != 0)
        {
            multiplier *= Math.Sqrt(2) - 0.001; // TODO tune
            if (MovementHelper.IsClimbable(startState))
            {
                // edging around doesn't work if doing so would climb a ladder or vine instead of moving sideways
                return;
            }
        }
        else
        {
            // only can sprint if not edging around
            if (context.CanSprint && !water)
            {
                multiplier *= ActionCosts.SprintMultiplier;
            }
        }
        res.Cost = multiplier * Math.Sqrt(2);
        if (descend)
        {
            res.Cost += Math.Max(ActionCosts.FallNBlocksCost[1], ActionCosts.CenterAfterFallCost);
            res.Y = y - 1;
        }
        else
        {
            res.Y = y;
        }
        res.X = destX;
        res.Z = destZ;
    }

    private double GetAscendCost(int x, int y, int z, int dx, int dz)
    {
        var destX = x + dx;
        var destZ = z + dz;
        var destY = y + 1;

        // Baritone MovementAscend.cost lines 68-70: Check destination floor
        var destFloor = context.GetBlockState(destX, destY - 1, destZ);
        if (destX == -1 && destZ >= 1)
        {
            Console.WriteLine(
                $"[AStar] Ascend Check ({destX},{destY},{destZ}): Floor destFloor@({destX},{destY - 1},{destZ}) is '{destFloor.Name}' (BlocksMotion={destFloor.BlocksMotion}, CanWalkOn={MovementHelper.CanWalkOn(destFloor)})");
        }

        double placeCost = 0;
        if (!MovementHelper.CanWalkOn(destFloor))
        {
            if (!context.AllowPlace || !context.HasThrowaway || !MovementHelper.IsReplaceable(destFloor))
            {
                return ActionCosts.CostInf;
            }

            // Baritone MovementAscend.cost lines 78-93: Check for placement option against adjacent blocks
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementAscend.java:78-93
            bool foundPlaceOption = false;
            // HORIZONTALS_BUT_ALSO_DOWN = North, South, East, West, Down
            int[][] directions = [[0, 0, -1], [0, 0, 1], [1, 0, 0], [-1, 0, 0], [0, -1, 0]];
            for (int i = 0; i < 5; i++)
            {
                int againstX = destX + directions[i][0];
                int againstY = (destY - 1) + directions[i][1];
                int againstZ = destZ + directions[i][2];
                // Baritone line 83-85: Skip if it's our source position
                if (againstX == x && againstZ == z)
                {
                    continue;
                }

                // Baritone line 86-88
                if (MovementHelper.CanPlaceAgainst(context.GetBlockState(againstX, againstY, againstZ)))
                {
                    foundPlaceOption = true;
                    break;
                }
            }

            // Baritone lines 91-93: If no placement option found, movement is impossible
            if (!foundPlaceOption)
            {
                return ActionCosts.CostInf;
            }

            placeCost = context.PlaceBlockCost + ActionCosts.WalkOneBlockCost;
        }

        // Baritone lines 114-117: Can't jump from ladder/vine, can't jump from bottom slab (except to another)
        var srcDown = context.GetBlockState(x, y - 1, z);
        if (MovementHelper.IsClimbable(srcDown)) return ActionCosts.CostInf;

        bool jumpingFromBottomSlab = MovementHelper.IsBottomSlab(srcDown);
        bool jumpingToBottomSlab = MovementHelper.IsBottomSlab(destFloor);

        // Baritone line 121-122: Can only ascend from bottom slab to another bottom slab
        if (jumpingFromBottomSlab && !jumpingToBottomSlab)
        {
            return ActionCosts.CostInf;
        }

        var destBody = context.GetBlockState(destX, destY, destZ);
        var destHead = context.GetBlockState(destX, destY + 1, destZ);

        // Baritone MovementAscend.cost: check jump clearance (x, y+2, z) and destination blocks
        var jumpSpace = context.GetBlockState(x, y + 2, z);

        double hardnessJump = GetMiningCost(jumpSpace);
        if (hardnessJump >= ActionCosts.CostInf) return ActionCosts.CostInf;

        double hardnessBody = GetMiningCost(destBody);
        if (hardnessBody >= ActionCosts.CostInf) return ActionCosts.CostInf;

        double hardnessHead = GetMiningCost(destHead);
        if (hardnessHead >= ActionCosts.CostInf) return ActionCosts.CostInf;

        // Base move cost - Baritone lines 124-142
        double cost;
        if (jumpingToBottomSlab)
        {
            if (jumpingFromBottomSlab)
            {
                cost = Math.Max(ActionCosts.JumpOneBlockCost, ActionCosts.WalkOneBlockCost);
                cost += context.JumpPenalty;
            }
            else
            {
                // Walking into a bottom slab, no jump needed
                cost = ActionCosts.WalkOneBlockCost;
            }
        }
        else
        {
            cost = Math.Max(ActionCosts.JumpOneBlockCost, ActionCosts.WalkOneBlockCost);
            cost += context.JumpPenalty;
        }

        // Check corners for diagonal ascend
        if (dx != 0 && dz != 0)
        {
            // For ascend, we need to check corners at y+1 (destination body) and y+2 (destination head)
            var cornerABody = context.GetBlockState(x + dx, y + 1, z);
            var cornerAHead = context.GetBlockState(x + dx, y + 2, z);

            var cornerBBody = context.GetBlockState(x, y + 1, z + dz);
            var cornerBHead = context.GetBlockState(x, y + 2, z + dz);

            bool canA = MovementHelper.CanWalkThrough(cornerABody) && MovementHelper.CanWalkThrough(cornerAHead);
            bool canB = MovementHelper.CanWalkThrough(cornerBBody) && MovementHelper.CanWalkThrough(cornerBHead);

            if (!canA && !canB) return ActionCosts.CostInf;
        }

        return cost + hardnessJump + hardnessBody + hardnessHead + placeCost;
    }

    /// <summary>
    /// Calculates the cost of a descend movement, potentially modifying the destination Y coordinate.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDescend.java:79-134
    /// </summary>
    private void GetDescendCost(int x, int y, int z, int destX, int destZ, MutableMoveResult res)
    {
        double totalCost = 0;
        var destDown = context.GetBlockState(destX, y - 1, destZ);
        totalCost += MovementHelper.GetMiningDurationTicks(context, destX, y - 1, destZ, destDown, false);
        if (totalCost >= ActionCosts.CostInf)
        {
            return;
        }
        totalCost += MovementHelper.GetMiningDurationTicks(context, destX, y, destZ, false);
        if (totalCost >= ActionCosts.CostInf)
        {
            return;
        }
        totalCost += MovementHelper.GetMiningDurationTicks(context, destX, y + 1, destZ, true); // only the top block in the 3 we need to mine needs to consider the falling blocks above
        if (totalCost >= ActionCosts.CostInf)
        {
            return;
        }

        var fromDown = context.GetBlockState(x, y - 1, z);
        if (MovementHelper.IsClimbable(fromDown))
        {
            return;
        }

        // Check if we can walk on the floor below destination
        var below = context.GetBlockState(destX, y - 2, destZ);
        if (!MovementHelper.CanWalkOn(below))
        {
            // Dynamic fall - scan downward for landing position
            DynamicFallCost(x, y, z, destX, destZ, totalCost, below, res);
            return;
        }

        if (MovementHelper.IsClimbable(destDown))
        {
            return;
        }
        if (MovementHelper.CanUseFrostWalker(context, destDown))
        {
            return; // the water will freeze when we try to walk into it
        }

        // we walk half the block plus 0.3 to get to the edge, then we walk the other 0.2 while simultaneously falling (math.max because of how it's in parallel)
        double walk = ActionCosts.WalkOffBlockCost;
        if (MovementHelper.IsSoulSand(fromDown))
        {
            // use this ratio to apply the soul sand speed penalty to our 0.8 block distance
            walk *= ActionCosts.WalkOneOverSoulSandCost / ActionCosts.WalkOneBlockCost;
        }
        totalCost += walk + Math.Max(ActionCosts.FallNBlocksCost[1], ActionCosts.CenterAfterFallCost);
        res.X = destX;
        res.Y = y - 1;
        res.Z = destZ;
        res.Cost = totalCost;
    }

    /// <summary>
    /// Calculates dynamic fall cost by scanning downward for a valid landing position.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDescend.java:136-223
    /// </summary>
    private bool DynamicFallCost(int x, int y, int z, int destX, int destZ, double frontBreak, BlockState? below, MutableMoveResult res)
    {
        // Note: We don't check for FallingBlock in C# as we don't have that type yet
        // This is a minor discrepancy but shouldn't affect most pathfinding scenarios
        
        if (!MovementHelper.CanWalkThrough(below))
        {
            return false;
        }
        double costSoFar = 0;
        int effectiveStartHeight = y;
        for (int fallHeight = 3; true; fallHeight++)
        {
            int newY = y - fallHeight;
            if (newY < context.MinY)
            {
                return false;
            }
            bool reachedMinimum = fallHeight >= context.MinFallHeight;
            var ontoBlock = context.GetBlockState(destX, newY, destZ);
            int unprotectedFallHeight = fallHeight - (y - effectiveStartHeight);
            double tentativeCost = ActionCosts.WalkOffBlockCost + ActionCosts.FallNBlocksCost[unprotectedFallHeight] + frontBreak + costSoFar;
            if (reachedMinimum && MovementHelper.IsWater(ontoBlock))
            {
                if (!MovementHelper.CanWalkThrough(ontoBlock))
                {
                    return false;
                }
                if (context.AssumeWalkOnWater)
                {
                    return false; // TODO fix
                }
                // Note: Flowing water check not implemented yet
                if (!MovementHelper.CanWalkOn(context.GetBlockState(destX, newY - 1, destZ)))
                {
                    return false;
                }
                // found a fall into water
                res.X = destX;
                res.Y = newY;
                res.Z = destZ;
                res.Cost = tentativeCost; // TODO incorporate water swim up cost?
                return false;
            }
            if (reachedMinimum && context.AllowFallIntoLava && MovementHelper.IsLava(ontoBlock))
            {
                // found a fall into lava
                res.X = destX;
                res.Y = newY;
                res.Z = destZ;
                res.Cost = tentativeCost;
                return false;
            }
            if (unprotectedFallHeight <= 11 && MovementHelper.IsClimbable(ontoBlock))
            {
                // if fall height is greater than or equal to 11, we don't actually grab on to vines or ladders
                costSoFar += ActionCosts.FallNBlocksCost[unprotectedFallHeight - 1];
                costSoFar += ActionCosts.LadderDownOneCost;
                effectiveStartHeight = newY;
                continue;
            }
            if (MovementHelper.CanWalkThrough(ontoBlock))
            {
                continue;
            }
            if (!MovementHelper.CanWalkOn(ontoBlock))
            {
                return false;
            }
            if (MovementHelper.IsBottomSlab(ontoBlock))
            {
                return false; // falling onto a half slab is really glitchy
            }
            if (reachedMinimum && unprotectedFallHeight <= context.MaxFallHeightNoWater + 1)
            {
                res.X = destX;
                res.Y = newY + 1;
                res.Z = destZ;
                res.Cost = tentativeCost;
                return false;
            }
            if (reachedMinimum && context.HasWaterBucket && unprotectedFallHeight <= context.MaxFallHeightBucket + 1)
            {
                res.X = destX;
                res.Y = newY + 1; // this is the block we're falling onto, so dest is +1
                res.Z = destZ;
                res.Cost = tentativeCost + context.CostOfPlacingAt(destX, newY, destZ); // placeBucketCost equivalent
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    private double GetPillarCost(int x, int y, int z)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementPillar.java:58-139
        var fromState = context.GetBlockState(x, y, z);
        bool ladder = MovementHelper.IsClimbable(fromState);
        var fromDown = context.GetBlockState(x, y - 1, z);

        // Baritone lines 63-70: Can't pillar from ladder/vine or bottom slab
        if (!ladder)
        {
            if (MovementHelper.IsClimbable(fromDown)) return ActionCosts.CostInf;
            if (MovementHelper.IsBottomSlab(fromDown)) return ActionCosts.CostInf;
        }

        // Baritone line 74: Check block at Y+2
        var toBreak = context.GetBlockState(x, y + 2, z);

        // Baritone lines 86-96: Calculate placement cost
        double placeCost = 0;
        if (!ladder)
        {
            placeCost = context.CostOfPlacingAt(x, y, z);
            if (placeCost >= ActionCosts.CostInf) return ActionCosts.CostInf;
        }

        // Baritone lines 107-109: Get mining hardness
        double hardness = 0;
        if (!MovementHelper.CanWalkThrough(context, x, y + 2, z))
        {
            hardness = GetMiningCost(toBreak);
            if (hardness >= ActionCosts.CostInf) return ActionCosts.CostInf;
        }

        // Baritone lines 134-138: Calculate final cost
        if (ladder)
        {
            return ActionCosts.LadderUpOneCost + hardness * 5;
        }
        else
        {
            return ActionCosts.JumpOneBlockCost + placeCost + context.JumpPenalty + hardness;
        }
    }

    private double GetDownwardCost(int x, int y, int z)
    {
        if (!context.AllowDownward) return ActionCosts.CostInf;

        // Destination floor must be walkable
        if (!MovementHelper.CanWalkOn(context.GetBlockState(x, y - 2, z))) return ActionCosts.CostInf;

        var down = context.GetBlockState(x, y - 1, z);
        if (MovementHelper.IsClimbable(down))
        {
            return ActionCosts.LadderDownOneCost;
        }
        else
        {
            double hardness = 0;
            if (!MovementHelper.CanWalkThrough(down))
            {
                hardness = GetMiningCost(down);
                if (hardness >= ActionCosts.CostInf) return ActionCosts.CostInf;
            }

            return ActionCosts.GetFallCost(1) + hardness;
        }
    }
}
