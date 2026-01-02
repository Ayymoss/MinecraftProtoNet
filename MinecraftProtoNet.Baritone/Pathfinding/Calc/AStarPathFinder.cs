using System.Diagnostics;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Pathfinding.Goals;
using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;
using Serilog;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

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
public class AStarPathFinder(CalculationContext context, IGoal goal, int startX, int startY, int startZ)
{
    private readonly Dictionary<long, PathNode> _nodeMap = new();
    private readonly BinaryHeapOpenSet _openSet = new();

    private volatile bool _cancelRequested;

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
        var startNode = GetOrCreateNode(startX, startY, startZ);
        startNode.Cost = 0;
        startNode.EstimatedCostToGoal = goal.Heuristic(startX, startY, startZ);
        startNode.CombinedCost = startNode.EstimatedCostToGoal;

        Log.Information("[Pathfinder] Starting A* from ({StartX}, {StartY}, {StartZ}) to goal with heuristic={Heuristic:F1}",
            startX, startY, startZ, startNode.EstimatedCostToGoal);

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
                if (newY < context.MinY || newY > context.MaxY) continue;

                // Chunk load check
                if ((newX >> 4 != currentNode.X >> 4 || newZ >> 4 != currentNode.Z >> 4) && !context.IsLoaded(newX, newZ))
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
                    neighbor.EstimatedCostToGoal = goal.Heuristic(newX, newY, newZ);
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
            var path = ReconstructPath(bestPartial, goal, numNodes);
            Log.Debug("[AStar] Partial Result: {NodeCount} nodes", path.Positions.Count);
            return (PathCalculationResultType.PartialSuccess, path);
        }

        return (PathCalculationResultType.Failure, null);
    }

    private Path ReconstructPath(PathNode endNode, IGoal goal, int numNodesConsidered)
    {
        var positions = new List<(int, int, int)>();
        var current = endNode;

        while (current != null)
        {
            positions.Add((current.X, current.Y, current.Z));
            current = current.Previous;
        }

        positions.Reverse();

        var reachesGoal = goal.IsInGoal(endNode.X, endNode.Y, endNode.Z);
        return new Path(positions, goal, numNodesConsidered, reachesGoal);
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
        var dx = node.X - startX;
        var dy = node.Y - startY;
        var dz = node.Z - startZ;
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
        // Use Centralized MovementParkour logic (Baritone parity)
        // This ensures checks for obstructions, valid landing spots, and correct costs are applied.
        var directions = new[] 
        { 
            MoveDirection.ParkourNorth, 
            MoveDirection.ParkourSouth, 
            MoveDirection.ParkourEast, 
            MoveDirection.ParkourWest 
        };

        foreach (var dir in directions)
        {
            foreach (var move in MovementParkour.CreateParkourMoves(context, current.X, current.Y, current.Z, dir))
            {
                // Calculate cost using the robust MovementParkour logic
                var cost = move.CalculateCost(context);
                
                if (cost < ActionCosts.CostInf)
                {
                    yield return (move.Destination.X - current.X, 
                                  move.Destination.Y - current.Y, 
                                  move.Destination.Z - current.Z, 
                                  cost);
                }
            }
        }
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
        
        for (int fallHeight = 2; fallHeight <= maxFall; fallHeight++)
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
        if (!MovementHelper.CanWalkOn(destFloor)) return ActionCosts.CostInf;

        var destBody = context.GetBlockState(destX, y, destZ);
        var destHead = context.GetBlockState(destX, y + 1, destZ);
        
        // Baritone lines 107-125: Calculate mining duration for blocking blocks
        double hardness1 = GetMiningCost(destBody);
        if (hardness1 >= ActionCosts.CostInf) return ActionCosts.CostInf;
        
        double hardness2 = GetMiningCost(destHead);
        if (hardness2 >= ActionCosts.CostInf) return ActionCosts.CostInf;

        var cost = context.CanSprint ? ActionCosts.SprintOneBlockCost : ActionCosts.WalkOneBlockCost;
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

    private double GetDiagonalCost(int x, int y, int z, int dx, int dy, int dz)
    {
        var destX = x + dx;
        var destY = y + dy;
        var destZ = z + dz;

        var destFloor = context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor)) return ActionCosts.CostInf;

        var destBody = context.GetBlockState(destX, destY, destZ);
        var destHead = context.GetBlockState(destX, destY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(destBody) || !MovementHelper.CanWalkThrough(destHead)) return ActionCosts.CostInf;

        // Baritone MovementDiagonal.cost lines 185-214: Check corner passability
        // pb0 = corner A at (x, y, destZ)
        // pb2 = corner B at (destX, y, z)
        var cornerABody = context.GetBlockState(x, y, z + dz);  // Corner A at source Y
        var cornerAMid = context.GetBlockState(x, y + 1, z + dz);  // Corner A mid
        var cornerBBody = context.GetBlockState(x + dx, y, z);  // Corner B at source Y
        var cornerBMid = context.GetBlockState(x + dx, y + 1, z);  // Corner B mid

        bool canALow = MovementHelper.CanWalkThrough(cornerABody);
        bool canAMid = MovementHelper.CanWalkThrough(cornerAMid);
        bool canBLow = MovementHelper.CanWalkThrough(cornerBBody);
        bool canBMid = MovementHelper.CanWalkThrough(cornerBMid);

        if (dy > 0)
        {
            // Diagonal ascending - Baritone lines 187-207
            // Need to check jump clearance and full 3-block column on corners
            var srcCeil = context.GetBlockState(x, y + 2, z);
            if (!MovementHelper.CanWalkThrough(srcCeil)) return ActionCosts.CostInf;
            
            var destCeil = context.GetBlockState(destX, y + 2, destZ);
            if (!MovementHelper.CanWalkThrough(destCeil)) return ActionCosts.CostInf;

            // Check corner tops for ascending (y+2 level)
            var cornerATop = context.GetBlockState(x, y + 2, z + dz);
            var cornerBTop = context.GetBlockState(x + dx, y + 2, z);
            
            bool canATop = MovementHelper.CanWalkThrough(cornerATop);
            bool canBTop = MovementHelper.CanWalkThrough(cornerBTop);
            
            // Baritone line 194: Need at least one corner with full 3-block clearance
            bool optionA = canATop && canAMid && canALow;
            bool optionB = canBTop && canBMid && canBLow;
            
            if (!optionA && !optionB)
            {
                return ActionCosts.CostInf; // No valid corner path
            }
            
            // Baritone lines 199-200: Check for head bonk scenarios
            // If top is blocked but mid/low are clear, we'd bonk our head during the jump
            if ((!canATop && canAMid && canALow) || (!canBTop && canBMid && canBLow))
            {
                return ActionCosts.CostInf; // Would bonk head
            }
        }
        else if (dy < 0)
        {
            // Descending diagonal - check corners at source level
            bool canA = canALow && canAMid;
            bool canB = canBLow && canBMid;
            
            if (!canA && !canB) return ActionCosts.CostInf;
        }
        else
        {
            // Same level diagonal - standard 2-block corner check
            bool canA = canALow && canAMid;
            bool canB = canBLow && canBMid;
            
            if (!canA && !canB) return ActionCosts.CostInf;
        }

        var multiplier = context.CanSprint ? ActionCosts.SprintMultiplier : 1.0;
        var cost = ActionCosts.WalkOneBlockCost * Math.Sqrt(2) * multiplier;
        
        if (dy > 0) cost += ActionCosts.JumpOneBlockCost + context.JumpPenalty;
        if (dy < 0) cost += ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(1) + ActionCosts.CenterAfterFallCost;

        return cost;
    }

    private double GetAscendCost(int x, int y, int z, int dx, int dz)
    {
        var destX = x + dx;
        var destZ = z + dz;
        var destY = y + 1;

        // Baritone MovementAscend.cost lines 68-70: Check destination floor
        var destFloor = context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor)) return ActionCosts.CostInf;

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

        return cost + hardnessJump + hardnessBody + hardnessHead;
    }

    private double GetDescendCost(int x, int y, int z, int dx, int dz)
    {
        var destX = x + dx;
        var destZ = z + dz;
        var destY = y - 1;

        var destFloor = context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor)) return ActionCosts.CostInf;

        var destBody = context.GetBlockState(destX, destY, destZ); // y - 1
        var destHead = context.GetBlockState(destX, destY + 1, destZ); // y
        var destAbove = context.GetBlockState(destX, destY + 2, destZ); // y + 1 (Baritone checks this too)

        double hardness1 = GetMiningCost(destBody);
        if (hardness1 >= ActionCosts.CostInf) return ActionCosts.CostInf;

        double hardness2 = GetMiningCost(destHead);
        if (hardness2 >= ActionCosts.CostInf) return ActionCosts.CostInf;

        double hardness3 = GetMiningCost(destAbove);
        if (hardness3 >= ActionCosts.CostInf) return ActionCosts.CostInf;

        // Check corners for diagonal descent
        if (dx != 0 && dz != 0)
        {
            var cornerABody = context.GetBlockState(x + dx, y, z);
            var cornerAHead = context.GetBlockState(x + dx, y + 1, z);
            
            var cornerBBody = context.GetBlockState(x, y, z + dz);
            var cornerBHead = context.GetBlockState(x, y + 1, z + dz);

            bool canA = MovementHelper.CanWalkThrough(cornerABody) && MovementHelper.CanWalkThrough(cornerAHead);
            bool canB = MovementHelper.CanWalkThrough(cornerBBody) && MovementHelper.CanWalkThrough(cornerBHead);
            
            // If neither corner is passable, we are blocked
            if (!canA && !canB) return ActionCosts.CostInf;
        }

        return ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(1) + ActionCosts.CenterAfterFallCost + hardness1 + hardness2 + hardness3;
    }

    private double GetPillarCost(int x, int y, int z)
    {
        var fromState = context.GetBlockState(x, y, z);
        bool ladder = MovementHelper.IsClimbable(fromState);
        var fromDown = context.GetBlockState(x, y - 1, z);
        
        if (!ladder)
        {
            if (MovementHelper.IsClimbable(fromDown)) return ActionCosts.CostInf;
            if (MovementHelper.IsBottomSlab(fromDown)) return ActionCosts.CostInf;
        }

        var toBreak = context.GetBlockState(x, y + 2, z);
        
        double placeCost = 0;
        if (!ladder)
        {
            placeCost = context.CostOfPlacingAt(x, y, z);
            if (placeCost >= ActionCosts.CostInf) return ActionCosts.CostInf;
        }

        double hardness = 0;
        if (!MovementHelper.CanWalkThrough(context, x, y + 2, z))
        {
            hardness = GetMiningCost(toBreak);
            if (hardness >= ActionCosts.CostInf) return ActionCosts.CostInf;
        }

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
