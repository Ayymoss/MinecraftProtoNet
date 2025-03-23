using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Handlers.Meta;

public class AStarPathFinder(Level level)
{
    private const int MaxSearchIterations = 1000;
    private const float DiagonalCost = 1.414f;
    private const int MaxJumpHeight = 1;
    private const int MaxFallHeight = 3;
    private const bool DebugMode = true; // Set to true for extra output

    /// <summary>
    /// Find a path from start to target position
    /// </summary>
    /// <returns>List of points in the path, or empty list if no path found</returns>
    public List<Vector3<double>>? FindPath(Vector3<double> start, Vector3<double> target, int maxIterations = MaxSearchIterations)
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

        if (DebugMode)
        {
            Console.WriteLine($"Finding path from {startBlock} to {targetBlock}");
        }

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

        var iterations = 0;

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            var current = openSet.Dequeue();
            var currentPos = (current.Position.X, current.Position.Y, current.Position.Z);

            if (DebugMode && iterations % 100 == 0)
            {
                Console.WriteLine($"Iteration {iterations}, processing node {current.Position}, F={current.F}");
            }

            if (current.Position.X == targetBlock.X && current.Position.Y == targetBlock.Y && current.Position.Z == targetBlock.Z)
            {
                if (DebugMode)
                {
                    Console.WriteLine($"Path found in {iterations} iterations");
                }

                return ReconstructPath(current);
            }

            closedSet.Add(currentPos);
            var neighbors = GetWalkableNeighbors(current.Position);

            if (DebugMode && neighbors.Count == 0)
            {
                Console.WriteLine($"No walkable neighbors for {current.Position}");
            }

            foreach (var neighbor in neighbors)
            {
                var neighborPos = (neighbor.X, neighbor.Y, neighbor.Z);
                if (closedSet.Contains(neighborPos)) continue;

                var tentativeG = current.G + CalculateDistance(current.Position, neighbor);

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

        if (DebugMode)
        {
            Console.WriteLine($"No path found after {iterations} iterations");
        }

        return null;
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
    private bool IsWalkable(int x, int y, int z)
    {
        var currentBlock = level.GetBlockAt(x, y, z);
        var blockAbove = level.GetBlockAt(x, y + 1, z);
        var blockBelow = level.GetBlockAt(x, y - 1, z);

        if (currentBlock == null || blockAbove == null) return false;

        // Position is walkable if:
        // 1. Current block is air or passable liquid
        // 2. Block above is air or passable (for headroom)
        // 3. Block below is solid (not air) unless in liquid

        var currentPassable = currentBlock.IsAir || currentBlock.IsLiquid;
        var hasHeadroom = blockAbove.IsAir || blockAbove.IsLiquid;

        if (currentBlock.IsLiquid) return hasHeadroom;

        var hasSolidGround = false;
        if (blockBelow != null)
        {
            hasSolidGround = blockBelow is { IsAir: false, IsLiquid: false };
        }

        return currentPassable && hasHeadroom && (hasSolidGround || blockBelow == null);
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

                if (dx != 0 && dz != 0 && !isInLiquid)
                {
                    if (!IsWalkable(position.X + dx, position.Y, position.Z) || !IsWalkable(position.X, position.Y, position.Z + dz))
                    {
                        continue;
                    }
                }

                if (IsWalkable(nx, position.Y, nz))
                {
                    neighbors.Add(new Vector3<int>(nx, position.Y, nz));
                }

                if (!isInLiquid)
                {
                    for (var dy = 1; dy <= MaxJumpHeight; dy++)
                    {
                        var blockToLand = level.GetBlockAt(nx, position.Y + dy, nz);
                        var blockAboveLanding = level.GetBlockAt(nx, position.Y + dy + 1, nz);

                        var canLand = blockToLand != null && blockAboveLanding != null &&
                                      (blockToLand.IsAir || blockToLand.IsLiquid) &&
                                      (blockAboveLanding.IsAir || blockAboveLanding.IsLiquid);

                        if (!canLand) continue;
                        var blockBelow = level.GetBlockAt(nx, position.Y + dy - 1, nz);
                        var hasSolidGround = blockBelow is { IsAir: false, IsLiquid: false };

                        if (!hasSolidGround && !blockToLand.IsLiquid) continue;
                        neighbors.Add(new Vector3<int>(nx, position.Y + dy, nz));
                        break;
                    }
                }

                for (var dy = 1; dy <= MaxFallHeight; dy++)
                {
                    if (!IsWalkable(nx, position.Y - dy, nz)) continue;
                    neighbors.Add(new Vector3<int>(nx, position.Y - dy, nz));
                    break;
                }
            }
        }

        if (!isInLiquid) return neighbors;

        if (IsWalkable(position.X, position.Y + 1, position.Z))
        {
            neighbors.Add(new Vector3<int>(position.X, position.Y + 1, position.Z));
        }

        if (IsWalkable(position.X, position.Y - 1, position.Z))
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
