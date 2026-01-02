namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// A binary min-heap implementation for the A* open set.
/// Based on Baritone's BinaryHeapOpenSet.java.
/// Provides O(log n) insert, O(log n) remove-min, and O(log n) update operations.
/// </summary>
public class BinaryHeapOpenSet
{
    private PathNode[] _heap;
    private int _size;

    /// <summary>
    /// Creates a new open set with default initial capacity.
    /// </summary>
    public BinaryHeapOpenSet() : this(1024)
    {
    }

    /// <summary>
    /// Creates a new open set with specified initial capacity.
    /// </summary>
    public BinaryHeapOpenSet(int initialCapacity)
    {
        _heap = new PathNode[initialCapacity];
        _size = 0;
    }

    /// <summary>
    /// Gets the number of nodes in the open set.
    /// </summary>
    public int Count => _size;

    /// <summary>
    /// Returns whether the open set is empty.
    /// </summary>
    public bool IsEmpty => _size == 0;

    /// <summary>
    /// Inserts a node into the open set.
    /// </summary>
    public void Insert(PathNode node)
    {
        if (_size >= _heap.Length)
        {
            Grow();
        }

        _heap[_size] = node;
        node.HeapIndex = _size;
        _size++;
        BubbleUp(_size - 1);
    }

    /// <summary>
    /// Updates a node's position in the heap after its cost has decreased.
    /// Only call this when the cost has decreased, not increased.
    /// </summary>
    public void Update(PathNode node)
    {
        BubbleUp(node.HeapIndex);
    }

    /// <summary>
    /// Removes and returns the node with the lowest combined cost.
    /// </summary>
    public PathNode RemoveLowest()
    {
        if (_size == 0)
        {
            throw new InvalidOperationException("Open set is empty");
        }

        var result = _heap[0];
        result.HeapIndex = -1; // Mark as no longer in heap

        _size--;
        if (_size > 0)
        {
            _heap[0] = _heap[_size];
            _heap[0].HeapIndex = 0;
            BubbleDown(0);
        }
        _heap[_size] = null!; // Help GC

        return result;
    }

    /// <summary>
    /// Peeks at the node with the lowest combined cost without removing it.
    /// </summary>
    public PathNode PeekLowest()
    {
        if (_size == 0)
        {
            throw new InvalidOperationException("Open set is empty");
        }
        return _heap[0];
    }

    /// <summary>
    /// Clears all nodes from the open set.
    /// </summary>
    public void Clear()
    {
        for (var i = 0; i < _size; i++)
        {
            _heap[i].HeapIndex = -1;
            _heap[i] = null!;
        }
        _size = 0;
    }

    private void BubbleUp(int index)
    {
        var node = _heap[index];
        while (index > 0)
        {
            var parentIndex = (index - 1) >> 1;
            var parent = _heap[parentIndex];

            if (node.CombinedCost >= parent.CombinedCost)
            {
                break;
            }

            // Move parent down
            _heap[index] = parent;
            parent.HeapIndex = index;
            index = parentIndex;
        }
        _heap[index] = node;
        node.HeapIndex = index;
    }

    private void BubbleDown(int index)
    {
        var node = _heap[index];
        var halfSize = _size >> 1;

        while (index < halfSize)
        {
            var leftChild = (index << 1) + 1;
            var rightChild = leftChild + 1;

            // Find the smaller child
            var smallerChild = leftChild;
            if (rightChild < _size && _heap[rightChild].CombinedCost < _heap[leftChild].CombinedCost)
            {
                smallerChild = rightChild;
            }

            var child = _heap[smallerChild];
            if (node.CombinedCost <= child.CombinedCost)
            {
                break;
            }

            // Move child up
            _heap[index] = child;
            child.HeapIndex = index;
            index = smallerChild;
        }
        _heap[index] = node;
        node.HeapIndex = index;
    }

    private void Grow()
    {
        var newCapacity = _heap.Length * 2;
        var newHeap = new PathNode[newCapacity];
        Array.Copy(_heap, newHeap, _heap.Length);
        _heap = newHeap;
    }
}
