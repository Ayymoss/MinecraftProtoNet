using System.Diagnostics;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Pathfinding.Goals;
using MinecraftProtoNet.Pathfinding.Movement;
using Serilog;

namespace MinecraftProtoNet.Pathfinding.Calc;

/// <summary>
/// Result of a path calculation.
/// </summary>
public enum PathCalculationResultType
{
    /// <summary>Path found that reaches the goal.</summary>
    Success,
    /// <summary>Partial path found (best effort).</summary>
    PartialSuccess,
    /// <summary>No path could be found.</summary>
    Failure,
    /// <summary>Calculation was cancelled.</summary>
    Cancelled,
    /// <summary>Calculation timed out.</summary>
    Timeout
}

/// <summary>
/// A* pathfinding algorithm implementation.
/// Based on Baritone's AStarPathFinder.java.
/// </summary>
public class AStarPathFinder
{
    private readonly CalculationContext _context;
    private readonly IGoal _goal;
    private readonly int _startX;
    private readonly int _startY;
    private readonly int _startZ;

    private readonly Dictionary<long, PathNode> _nodeMap = new();
    private readonly BinaryHeapOpenSet _openSet = new();

    private volatile bool _cancelRequested;
    private PathNode? _bestSoFar;

    // Coefficients for tracking best partial paths
    private static readonly double[] Coefficients = { 1.5, 2.0, 2.5, 3.0, 4.0, 5.0 };
    private readonly PathNode?[] _bestByCoefficient = new PathNode?[Coefficients.Length];
    private readonly double[] _bestHeuristicByCoefficient = new double[Coefficients.Length];

    /// <summary>
    /// Minimum distance from start required for a valid partial path.
    /// </summary>
    private const double MinDistPath = 5.0;

    /// <summary>
    /// Time check interval (check timeout every N nodes).
    /// </summary>
    private const int TimeCheckInterval = 64;

    public AStarPathFinder(CalculationContext context, IGoal goal, int startX, int startY, int startZ)
    {
        _context = context;
        _goal = goal;
        _startX = startX;
        _startY = startY;
        _startZ = startZ;
    }

    /// <summary>
    /// Requests cancellation of the current calculation.
    /// </summary>
    public void Cancel()
    {
        _cancelRequested = true;
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
        var startNode = GetOrCreateNode(_startX, _startY, _startZ);
        startNode.Cost = 0;
        startNode.EstimatedCostToGoal = _goal.Heuristic(_startX, _startY, _startZ);
        startNode.CombinedCost = startNode.EstimatedCostToGoal;

        Log.Information("[Pathfinder] Starting A* from ({StartX}, {StartY}, {StartZ}) to goal with heuristic={Heuristic:F1}",
            _startX, _startY, _startZ, startNode.EstimatedCostToGoal);

        _openSet.Insert(startNode);

        // Initialize best tracking
        for (var i = 0; i < Coefficients.Length; i++)
        {
            _bestHeuristicByCoefficient[i] = startNode.EstimatedCostToGoal;
            _bestByCoefficient[i] = startNode;
        }

        var numNodes = 0;
        var numMovementsConsidered = 0;
        var failing = true; // Haven't found a good partial path yet

        while (!_openSet.IsEmpty && !_cancelRequested)
        {
            // Periodic timeout check
            if ((numNodes & (TimeCheckInterval - 1)) == 0)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed >= failureTimeout || (!failing && elapsed >= primaryTimeout))
                {
                    Log.Debug("[AStar] Timeout reached at {NumNodes} nodes. Failing={Failing}, Elapsed={Elapsed}ms",
                        numNodes, failing, elapsed);
                    break;
                }
            }

            var currentNode = _openSet.RemoveLowest();
            numNodes++;

            // Expand neighbors
            foreach (var (dx, dy, dz, cost) in GetPossibleMoves(currentNode))
            {
                var newX = currentNode.X + dx;
                var newY = currentNode.Y + dy;
                var newZ = currentNode.Z + dz;

                // Bounds check
                if (newY < _context.MinY || newY > _context.MaxY) continue;

                // Chunk load check
                if ((newX >> 4 != currentNode.X >> 4 || newZ >> 4 != currentNode.Z >> 4) && !_context.IsLoaded(newX, newZ))
                {
                    continue;
                }

                numMovementsConsidered++;
                if (cost >= ActionCosts.CostInf) continue;

                var actionCost = cost;
                
                // Baritone AStarPathFinder.java line 161 (isFavoring)
                // We don't have favoring implemented yet, but we'll stick to the structure.

                var neighbor = GetOrCreateNode(newX, newY, newZ);
                var tentativeCost = currentNode.Cost + actionCost;

                if (tentativeCost < neighbor.Cost)
                {
                    neighbor.Previous = currentNode;
                    neighbor.Cost = tentativeCost;
                    neighbor.EstimatedCostToGoal = _goal.Heuristic(newX, newY, newZ);
                    neighbor.CombinedCost = tentativeCost + neighbor.EstimatedCostToGoal;

                    if (neighbor.IsOpen())
                    {
                        _openSet.Update(neighbor);
                    }
                    else
                    {
                        _openSet.Insert(neighbor);
                    }

                    // Track best partial paths
                    UpdateBestSoFar(neighbor, ref failing);
                }
            }
        }

        Log.Debug("[AStar] Loop finished. Nodes={NumNodes}, Movements={Movements}, OpenSetEmpty={OpenSetEmpty}, Canceled={Canceled}",
            numNodes, numMovementsConsidered, _openSet.IsEmpty, _cancelRequested);

        if (_cancelRequested)
        {
            return (PathCalculationResultType.Cancelled, null);
        }

        // Return best partial path if we have one
        var bestPartial = GetBestPartialPath();
        if (bestPartial != null)
        {
            var path = Path.FromEndNode(bestPartial, _goal, numNodes);
            Log.Debug("[AStar] Partial Result: {NodeCount} nodes", path.Positions.Count);
            return (PathCalculationResultType.PartialSuccess, path);
        }

        return (PathCalculationResultType.Failure, null);
    }

    private void UpdateBestSoFar(PathNode node, ref bool failing)
    {
        for (var i = 0; i < Coefficients.Length; i++)
        {
            var heuristic = node.EstimatedCostToGoal + node.Cost / Coefficients[i];
            if (heuristic < _bestHeuristicByCoefficient[i])
            {
                _bestHeuristicByCoefficient[i] = heuristic;
                _bestByCoefficient[i] = node;

                if (failing && GetDistFromStartSq(node) > MinDistPath * MinDistPath)
                {
                    failing = false;
                }
            }
        }
    }

    private double GetDistFromStartSq(PathNode node)
    {
        var dx = node.X - _startX;
        var dy = node.Y - _startY;
        var dz = node.Z - _startZ;
        return dx * dx + dy * dy + dz * dz;
    }

    private PathNode? GetBestPartialPath()
    {
        // Find the best partial path from our tracked candidates
        PathNode? best = null;
        var bestHeuristic = double.MaxValue;

        foreach (var node in _bestByCoefficient)
        {
            if (node != null && node.EstimatedCostToGoal < bestHeuristic)
            {
                bestHeuristic = node.EstimatedCostToGoal;
                best = node;
            }
        }

        return best;
    }

    private PathNode GetOrCreateNode(int x, int y, int z)
    {
        var hash = PathNode.CalculateHash(x, y, z);
        if (!_nodeMap.TryGetValue(hash, out var node))
        {
            node = new PathNode(x, y, z);
            node.EstimatedCostToGoal = _goal.Heuristic(x, y, z);
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
    }

    private IEnumerable<(int dx, int dy, int dz, double cost)> GetCardinalMoves(PathNode current)
    {
        yield return (1, 0, 0, GetTraverseCost(current.X, current.Y, current.Z, 1, 0));
        yield return (-1, 0, 0, GetTraverseCost(current.X, current.Y, current.Z, -1, 0));
        yield return (0, 0, 1, GetTraverseCost(current.X, current.Y, current.Z, 0, 1));
        yield return (0, 0, -1, GetTraverseCost(current.X, current.Y, current.Z, 0, -1));

        yield return (1, 1, 0, GetAscendCost(current.X, current.Y, current.Z, 1, 0));
        yield return (-1, 1, 0, GetAscendCost(current.X, current.Y, current.Z, -1, 0));
        yield return (0, 1, 1, GetAscendCost(current.X, current.Y, current.Z, 0, 1));
        yield return (0, 1, -1, GetAscendCost(current.X, current.Y, current.Z, 0, -1));

        yield return (1, -1, 0, GetDescendCost(current.X, current.Y, current.Z, 1, 0));
        yield return (-1, -1, 0, GetDescendCost(current.X, current.Y, current.Z, -1, 0));
        yield return (0, -1, 1, GetDescendCost(current.X, current.Y, current.Z, 0, 1));
        yield return (0, -1, -1, GetDescendCost(current.X, current.Y, current.Z, 0, -1));
    }

    private IEnumerable<(int dx, int dy, int dz, double cost)> GetDiagonalMoves(PathNode current)
    {
        int[] dxs = { 1, 1, -1, -1 };
        int[] dzs = { 1, -1, 1, -1 };

        for (int i = 0; i < 4; i++)
        {
            var dx = dxs[i];
            var dz = dzs[i];

            // Same level
            yield return (dx, 0, dz, GetDiagonalCost(current.X, current.Y, current.Z, dx, 0, dz));
            
            // Ascend
            yield return (dx, 1, dz, GetDiagonalCost(current.X, current.Y, current.Z, dx, 1, dz));
            
            // Descend
            yield return (dx, -1, dz, GetDiagonalCost(current.X, current.Y, current.Z, dx, -1, dz));
        }
    }

    private IEnumerable<(int dx, int dy, int dz, double cost)> GetParkourMoves(PathNode current)
    {
        // Simplistic 2-block gap jump (Parkour)
        int[] dxs = { 1, -1, 0, 0 };
        int[] dzs = { 0, 0, 1, -1 };

        for (int i = 0; i < 4; i++)
        {
            var dx = dxs[i] * 2;
            var dz = dzs[i] * 2;
            
            var destFloor = _context.GetBlockState(current.X + dx, current.Y - 1, current.Z + dz);
            var inter1Body = _context.GetBlockState(current.X + dxs[i], current.Y, current.Z + dzs[i]);
            var inter1Head = _context.GetBlockState(current.X + dxs[i], current.Y + 1, current.Z + dzs[i]);
            
            if (MovementHelper.CanWalkOn(destFloor) && 
                MovementHelper.CanWalkThrough(inter1Body) && 
                MovementHelper.CanWalkThrough(inter1Head))
            {
                var cost = ActionCosts.WalkOneBlockCost * 2 + ActionCosts.JumpOneBlockCost;
                yield return (dx, 0, dz, cost);
            }
        }
    }

    private double GetTraverseCost(int x, int y, int z, int dx, int dz)
    {
        var destX = x + dx;
        var destZ = z + dz;

        var destFloor = _context.GetBlockState(destX, y - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor)) return ActionCosts.CostInf;

        var destBody = _context.GetBlockState(destX, y, destZ);
        var destHead = _context.GetBlockState(destX, y + 1, destZ);
        
        // Baritone lines 107-125: Calculate mining duration for blocking blocks
        double hardness1 = GetMiningCost(destBody);
        if (hardness1 >= ActionCosts.CostInf) return ActionCosts.CostInf;
        
        double hardness2 = GetMiningCost(destHead);
        if (hardness2 >= ActionCosts.CostInf) return ActionCosts.CostInf;

        var cost = _context.CanSprint ? ActionCosts.SprintOneBlockCost : ActionCosts.WalkOneBlockCost;
        if (MovementHelper.IsSoulSand(destFloor)) cost *= 2;
        
        // If we need to break blocks, can't sprint
        if (hardness1 > 0 || hardness2 > 0)
        {
            cost = ActionCosts.WalkOneBlockCost; // Can't sprint when breaking
        }
        
        return cost + hardness1 + hardness2;
    }
    
    /// <summary>
    /// Gets the mining cost for a block. Returns 0 if walkable, CostInf if unbreakable.
    /// </summary>
    private double GetMiningCost(BlockState block)
    {
        if (MovementHelper.CanWalkThrough(block)) return 0;
        if (!_context.AllowBreak) return ActionCosts.CostInf;
        
        // Calculate mining duration
        float toolSpeed = _context.GetBestToolSpeed?.Invoke(block) ?? 1.0f;
        var duration = ActionCosts.CalculateMiningDuration(toolSpeed, block.DestroySpeed, true);
        
        // Unbreakable blocks (bedrock, barriers, etc.)
        if (duration >= ActionCosts.CostInf || block.DestroySpeed < 0)
        {
            return ActionCosts.CostInf;
        }
        
        return duration;
    }

    private double GetDiagonalCost(int x, int y, int z, int dx, int dy, int dz)
    {
        var destX = x + dx;
        var destY = y + dy;
        var destZ = z + dz;

        var destFloor = _context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor)) return ActionCosts.CostInf;

        var destBody = _context.GetBlockState(destX, destY, destZ);
        var destHead = _context.GetBlockState(destX, destY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(destBody) || !MovementHelper.CanWalkThrough(destHead)) return ActionCosts.CostInf;

        var cornerABody = _context.GetBlockState(x + dx, y, z);
        var cornerAHead = _context.GetBlockState(x + dx, y + 1, z);
        var cornerBBody = _context.GetBlockState(x, y, z + dz);
        var cornerBHead = _context.GetBlockState(x, y + 1, z + dz);

        bool canA = MovementHelper.CanWalkThrough(cornerABody) && MovementHelper.CanWalkThrough(cornerAHead);
        bool canB = MovementHelper.CanWalkThrough(cornerBBody) && MovementHelper.CanWalkThrough(cornerBHead);
        
        if (!canA && !canB) return ActionCosts.CostInf;

        if (dy > 0)
        {
            var srcCeil = _context.GetBlockState(x, y + 2, z);
            if (!MovementHelper.CanWalkThrough(srcCeil)) return ActionCosts.CostInf;

            var cornerACeil = _context.GetBlockState(x + dx, y + 2, z);
            var cornerBCeil = _context.GetBlockState(x, y + 2, z + dz);
            
            bool canACeil = canA && MovementHelper.CanWalkThrough(cornerACeil);
            bool canBCeil = canB && MovementHelper.CanWalkThrough(cornerBCeil);
            
            if (!canACeil && !canBCeil) return ActionCosts.CostInf;
        }

        var multiplier = _context.CanSprint ? ActionCosts.SprintMultiplier : 1.0;
        var cost = ActionCosts.WalkOneBlockCost * Math.Sqrt(2) * multiplier;
        
        if (dy > 0) cost += ActionCosts.JumpOneBlockCost + _context.JumpPenalty;
        if (dy < 0) cost += ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(1) + ActionCosts.CenterAfterFallCost;

        return cost;
    }

    private double GetAscendCost(int x, int y, int z, int dx, int dz)
    {
        var destX = x + dx;
        var destZ = z + dz;
        var destY = y + 1;

        var destFloor = _context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor)) return ActionCosts.CostInf;

        var destBody = _context.GetBlockState(destX, destY, destZ);
        var destHead = _context.GetBlockState(destX, destY + 1, destZ);
        
        // Baritone MovementAscend.cost: check jump clearance (x, y+2, z) and destination blocks
        var jumpSpace = _context.GetBlockState(x, y + 2, z);

        double hardnessJump = GetMiningCost(jumpSpace);
        if (hardnessJump >= ActionCosts.CostInf) return ActionCosts.CostInf;

        double hardnessBody = GetMiningCost(destBody);
        if (hardnessBody >= ActionCosts.CostInf) return ActionCosts.CostInf;

        double hardnessHead = GetMiningCost(destHead);
        if (hardnessHead >= ActionCosts.CostInf) return ActionCosts.CostInf;

        // Base move cost
        double cost = ActionCosts.JumpOneBlockCost + _context.JumpPenalty;
        
        // If we are breaking blocks, we generally can't sprint jump efficiently? 
        // Baritone doesn't explicitly disable sprinting here but cost calculation implies duration addition.
        // We will sum durations.

        return cost + hardnessJump + hardnessBody + hardnessHead;
    }

    private double GetDescendCost(int x, int y, int z, int dx, int dz)
    {
        var destX = x + dx;
        var destZ = z + dz;
        var destY = y - 1;

        var destFloor = _context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor)) return ActionCosts.CostInf;

        var destBody = _context.GetBlockState(destX, destY, destZ); // y - 1
        var destHead = _context.GetBlockState(destX, destY + 1, destZ); // y
        var destAbove = _context.GetBlockState(destX, destY + 2, destZ); // y + 1 (Baritone checks this too)

        double hardness1 = GetMiningCost(destBody);
        if (hardness1 >= ActionCosts.CostInf) return ActionCosts.CostInf;

        double hardness2 = GetMiningCost(destHead);
        if (hardness2 >= ActionCosts.CostInf) return ActionCosts.CostInf;

        double hardness3 = GetMiningCost(destAbove);
        if (hardness3 >= ActionCosts.CostInf) return ActionCosts.CostInf;

        return ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(1) + ActionCosts.CenterAfterFallCost + hardness1 + hardness2 + hardness3;
    }

    private double GetPillarCost(int x, int y, int z)
    {
        var fromState = _context.GetBlockState(x, y, z);
        bool ladder = MovementHelper.IsClimbable(fromState);
        var fromDown = _context.GetBlockState(x, y - 1, z);
        
        if (!ladder)
        {
            if (MovementHelper.IsClimbable(fromDown)) return ActionCosts.CostInf;
            if (MovementHelper.IsBottomSlab(fromDown)) return ActionCosts.CostInf;
        }

        var toBreak = _context.GetBlockState(x, y + 2, z);
        // Simplified skip for fence gates for now
        
        double placeCost = 0;
        if (!ladder)
        {
            // Use CostOfPlacingAt which checks HasThrowaway and AllowPlace
            placeCost = _context.CostOfPlacingAt(x, y - 1, z); // Placing at current feet position happens after jumping? 
            // Baritone Pillar cost logic:
            // It costs COST_INF if we can't place.
            // Baritone context.costOfPlacingAt checks context.hasThrowaway.
            
            // Actually, for Pillar, we are placing under ourselves.
            // But we need to check if we can place.
            // In AStarPathFinder.java:234 (getPillarCost)
            // double placeCost = context.costOfPlacingAt(x, y, z, fromState); 
            // Wait, placing at (x, y, z) is placing AT the current block, which we are standing IN?
            // No, pillar means we are at (x,y,z), we jump to (x,y+1,z), and place at (x,y,z).
            // So we place at the source node's coordinates.
            
            placeCost = _context.CostOfPlacingAt(x, y, z);
            if (placeCost >= ActionCosts.CostInf) return ActionCosts.CostInf;
        }

        double hardness = 0;
        if (!MovementHelper.CanWalkThrough(_context, x, y + 2, z))
        {
            if (!_context.AllowBreak) return ActionCosts.CostInf;
            hardness = ActionCosts.WalkOneBlockCost * 3; // Placeholder for mining duration
        }

        if (ladder)
        {
            return ActionCosts.LadderUpOneCost + hardness * 5;
        }
        else
        {
            return ActionCosts.JumpOneBlockCost + placeCost + _context.JumpPenalty + hardness;
        }
    }

    private double GetDownwardCost(int x, int y, int z)
    {
        if (!_context.AllowDownward) return ActionCosts.CostInf;
        
        // Destination floor must be walkable
        if (!MovementHelper.CanWalkOn(_context.GetBlockState(x, y - 2, z))) return ActionCosts.CostInf;

        var down = _context.GetBlockState(x, y - 1, z);
        if (MovementHelper.IsClimbable(down))
        {
            return ActionCosts.LadderDownOneCost;
        }
        else
        {
            double hardness = 0;
            if (!MovementHelper.CanWalkThrough(down))
            {
                if (!_context.AllowBreak) return ActionCosts.CostInf;
                hardness = ActionCosts.WalkOneBlockCost * 3;
            }
            return ActionCosts.GetFallCost(1) + hardness;
        }
    }
}
