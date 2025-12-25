using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Handlers.Meta;

public class AStarPathFinder(Level level)
{
    private const int MaxSearchIterations = 2000;
    private const float DiagonalCost = 1.414f;
    private const int MaxJumpHeight = 1;
    private const int MaxFallHeight = 3;

    public record struct PathResult(List<Vector3<double>>? Path, bool ReachedTarget, float DistanceToTarget);

    /// <summary>
    /// Find a path from start to target position
    /// </summary>
    /// <returns>A PathResult containing the points, reach status, and final distance.</returns>
    public PathResult FindPath(Vector3<double> start, Vector3<double> target, int maxIterations = MaxSearchIterations)
    {
        var startBlock = new Vector3<int>(
            (int)Math.Floor(start.X),
            (int)Math.Floor(start.Y),
            (int)Math.Floor(start.Z)
        );

        var targetBlock = new Vector3<int>(
            (int)Math.Floor(target.X),
            (int)Math.Floor(target.Y),
            (int)Math.Floor(target.Z)
        );

        var openSet = new PriorityQueue<PathNode>();
        var closedSet = new HashSet<(int x, int y, int z)>();
        var nodeCache = new Dictionary<(int x, int y, int z), PathNode>();
        
        var startNode = new PathNode(startBlock)
        {
            G = 0,
            H = CalculateHeuristic(startBlock, targetBlock)
        };

        openSet.Enqueue(startNode);
        nodeCache[(startBlock.X, startBlock.Y, startBlock.Z)] = startNode;

        PathNode? bestNode = startNode;
        var iterations = 0;
        
        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            var current = openSet.Dequeue();
            var currentPos = (current.Position.X, current.Position.Y, current.Position.Z);

            if (current.H < bestNode.H)
            {
                bestNode = current;
            }

            if (current.Position.X == targetBlock.X && current.Position.Y == targetBlock.Y && current.Position.Z == targetBlock.Z)
            {
                var path = ReconstructPath(current);
                return new PathResult(path, true, 0);
            }

            closedSet.Add(currentPos);
            var neighbors = GetWalkableNeighbors(current.Position);

            foreach (var neighbor in neighbors)
            {
                var neighborPos = (neighbor.X, neighbor.Y, neighbor.Z);
                if (closedSet.Contains(neighborPos)) continue;

                var neighborType = GetPathType(neighbor.X, neighbor.Y, neighbor.Z);
                var malus = neighborType.GetMalus();
                
                // If malus is -1, it's effectively blocked
                if (malus < 0) continue;

                var tentativeG = current.G + CalculateDistance(current.Position, neighbor) + malus;

                if (!nodeCache.TryGetValue(neighborPos, out var neighborNode))
                {
                    neighborNode = new PathNode(neighbor)
                    {
                        H = CalculateHeuristic(neighbor, targetBlock)
                    };
                    nodeCache[neighborPos] = neighborNode;
                }

                if (!(tentativeG < neighborNode.G)) continue;

                neighborNode.Parent = current;
                neighborNode.G = tentativeG;

                if (!openSet.Contains(neighborNode)) openSet.Enqueue(neighborNode);
                else openSet.Update(neighborNode);
            }
        }

        // Return the best found partial path
        var partialPath = ReconstructPath(bestNode);
        return new PathResult(partialPath, false, bestNode.H);
    }

    /// <summary>
    /// Reconstruct path from end node to start node
    /// </summary>
    private static List<Vector3<double>> ReconstructPath(PathNode? endNode)
    {
        var path = new List<Vector3<double>>();
        var current = endNode;

        while (current != null)
        {
            path.Add(new Vector3<double>(
                current.Position.X + 0.5,
                current.Position.Y,
                current.Position.Z + 0.5
            ));
            current = current.Parent;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Calculate Manhattan distance heuristic 
    /// </summary>
    private static float CalculateHeuristic(Vector3<int> a, Vector3<int> b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
    }

    /// <summary>
    /// Calculate actual distance between adjacent nodes
    /// </summary>
    private static float CalculateDistance(Vector3<int> a, Vector3<int> b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        var dz = Math.Abs(a.Z - b.Z);
        var verticalCost = dy > 0 ? 1.0f : 0.0f;

        return dx switch
        {
            > 0 when dz > 0 => DiagonalCost + verticalCost,
            0 when dz == 0 => 1.0f,
            _ => 1.0f + verticalCost
        };
    }

    /// <summary>
    /// Check if position is walkable
    /// </summary>
    /// <summary>
    /// Gets the path type at a position, considering the entity's height.
    /// </summary>
    private PathType GetPathType(int x, int y, int z)
    {
        var feetType = PathfindingContext.GetPathTypeFromState(level.GetBlockAt(x, y, z));
        var headType = PathfindingContext.GetPathTypeFromState(level.GetBlockAt(x, y + 1, z));

        // If either block is definitively blocked, the position is blocked.
        if (feetType.GetMalus() < 0 || headType.GetMalus() < 0)
            return PathType.Blocked;

        // If either is dangerous, return the dangerous one (prioritize damage over movement)
        if (feetType == PathType.Lava || headType == PathType.Lava) return PathType.Lava;
        if (feetType == PathType.DamageFire || headType == PathType.DamageFire) return PathType.DamageFire;
        if (feetType == PathType.DamageOther || headType == PathType.DamageOther) return PathType.DamageOther;

        var result = feetType;
        if (feetType == PathType.Water && headType == PathType.Open)
        {
            result = PathType.WaterBorder; // Used here to represent "Surface Water"
        }

        // Add wall proximity check: only apply if the current result is relatively safe.
        // We only upgrade to WallNeighbor if WallNeighbor's malus is higher than current.
        if (result.GetMalus() < PathType.WallNeighbor.GetMalus())
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    
                    var nFeet = PathfindingContext.GetPathTypeFromState(level.GetBlockAt(x + dx, y, z + dz));
                    var nHead = PathfindingContext.GetPathTypeFromState(level.GetBlockAt(x + dx, y + 1, z + dz));
                    
                    if (nFeet == PathType.Blocked || nHead == PathType.Blocked)
                    {
                        return PathType.WallNeighbor;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Check if a position is in a liquid
    /// </summary>
    private bool IsInLiquid(int x, int y, int z)
    {
        var block = level.GetBlockAt(x, y, z);
        return block is { IsLiquid: true };
    }

    /// <summary>
    /// Get all walkable neighbors of a position
    /// </summary>
    private List<Vector3<int>> GetWalkableNeighbors(Vector3<int> position)
    {
        var neighbors = new List<Vector3<int>>();
        var isInLiquid = IsInLiquid(position.X, position.Y, position.Z);

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;

                var nx = position.X + dx;
                var nz = position.Z + dz;

                // Diagonal movement check (can't move diagonally if both sides are blocked)
                // Diagonal movement check (can't move diagonally if both sides are blocked)
                if (dx != 0 && dz != 0 && !isInLiquid)
                {
                    // Check horizontal gap for ALL moves at current level and target level
                    // If either is blocked, we can't squeeze through.
                    if (GetPathType(position.X + dx, position.Y, position.Z) == PathType.Blocked || 
                        GetPathType(position.X, position.Y, position.Z + dz) == PathType.Blocked ||
                        GetPathType(position.X + dx, position.Y + 1, position.Z) == PathType.Blocked || 
                        GetPathType(position.X, position.Y + 1, position.Z + dz) == PathType.Blocked)
                    {
                        continue;
                    }
                }

                // Level move
                if (GetPathType(nx, position.Y, nz) != PathType.Blocked)
                {
                    var below = level.GetBlockAt(nx, position.Y - 1, nz);
                    if (PathfindingContext.GetPathTypeFromState(below) == PathType.Blocked || isInLiquid)
                    {
                        neighbors.Add(new Vector3<int>(nx, position.Y, nz));
                    }
                }

                // Jump move
                // If in liquid, we allow jumping (bobbing) to exit water onto a ledge.
                // If NOT in liquid, we MUST have support (solid block below) to jump.
                var belowCurrent = level.GetBlockAt(position.X, position.Y - 1, position.Z);
                var isSupported = PathfindingContext.GetPathTypeFromState(belowCurrent) == PathType.Blocked;

                if (isInLiquid || isSupported)
                {
                    for (var dy = 1; dy <= MaxJumpHeight; dy++)
                    {
                        if (GetPathType(nx, position.Y + dy, nz) != PathType.Blocked)
                        {
                            var belowJump = level.GetBlockAt(nx, position.Y + dy - 1, nz);
                            if (PathfindingContext.GetPathTypeFromState(belowJump) == PathType.Blocked)
                            {
                                neighbors.Add(new Vector3<int>(nx, position.Y + dy, nz));
                                break;
                            }
                        }
                    }
                }

                // Fall move
                for (var dy = 1; dy <= MaxFallHeight; dy++)
                {
                    if (GetPathType(nx, position.Y - dy, nz) != PathType.Blocked)
                    {
                        var belowFall = level.GetBlockAt(nx, position.Y - dy - 1, nz);
                        if (PathfindingContext.GetPathTypeFromState(belowFall) == PathType.Blocked)
                        {
                            neighbors.Add(new Vector3<int>(nx, position.Y - dy, nz));
                            break;
                        }
                    }
                }
            }
        }

        if (!isInLiquid) return neighbors;

        if (GetPathType(position.X, position.Y + 1, position.Z) != PathType.Blocked)
        {
            neighbors.Add(new Vector3<int>(position.X, position.Y + 1, position.Z));
        }

        if (GetPathType(position.X, position.Y - 1, position.Z) != PathType.Blocked)
        {
            neighbors.Add(new Vector3<int>(position.X, position.Y - 1, position.Z));
        }

        return neighbors;
    }

    /// <summary>
    /// Node class for A* pathfinding
    /// </summary>
    private class PathNode(Vector3<int> position) : IComparable<PathNode>
    {
        public Vector3<int> Position { get; } = position;
        public PathNode Parent { get; set; }
        public float G { get; set; } = float.PositiveInfinity;
        public float H { get; set; } = 0;
        public float F => G + H;

        public int CompareTo(PathNode other)
        {
            return F.CompareTo(other.F);
        }

        public override string ToString()
        {
            return $"Node({Position.X},{Position.Y},{Position.Z}) G={G:F2} H={H:F2} F={F:F2}";
        }
    }

    /// <summary>
    /// Priority queue for A* open set
    /// </summary>
    private class PriorityQueue<T> where T : IComparable<T>
    {
        private readonly List<T> _heap = [];
        private readonly Dictionary<T, int> _indices = new();

        public int Count => _heap.Count;

        public void Enqueue(T item)
        {
            _heap.Add(item);
            _indices[item] = _heap.Count - 1;
            SiftUp(_heap.Count - 1);
        }

        public T Dequeue()
        {
            var result = _heap[0];
            _indices.Remove(result);

            if (_heap.Count > 1)
            {
                var last = _heap[^1];
                _heap[0] = last;
                _indices[last] = 0;
                _heap.RemoveAt(_heap.Count - 1);
                SiftDown(0);
            }
            else
            {
                _heap.RemoveAt(0);
            }

            return result;
        }

        public bool Contains(T item)
        {
            return _indices.ContainsKey(item);
        }

        public void Update(T item)
        {
            if (!_indices.TryGetValue(item, out var index)) return;

            SiftUp(index);
            SiftDown(index);
        }

        private void SiftUp(int index)
        {
            var parentIndex = (index - 1) / 2;

            while (index > 0 && _heap[index].CompareTo(_heap[parentIndex]) < 0)
            {
                Swap(index, parentIndex);
                index = parentIndex;
                parentIndex = (index - 1) / 2;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                var leftChild = index * 2 + 1;
                var rightChild = index * 2 + 2;
                var smallest = index;

                if (leftChild < _heap.Count && _heap[leftChild].CompareTo(_heap[smallest]) < 0) smallest = leftChild;
                if (rightChild < _heap.Count && _heap[rightChild].CompareTo(_heap[smallest]) < 0) smallest = rightChild;

                if (smallest != index)
                {
                    Swap(index, smallest);
                    index = smallest;
                    continue;
                }

                break;
            }
        }

        private void Swap(int i, int j)
        {
            (_heap[i], _heap[j]) = (_heap[j], _heap[i]);
            _indices[_heap[i]] = i;
            _indices[_heap[j]] = j;
        }
    }
}
