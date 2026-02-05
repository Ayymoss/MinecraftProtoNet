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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/openset/BinaryHeapOpenSet.java
 */

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc.OpenSet;

/// <summary>
/// A binary heap implementation of an open set. This is the one used in the AStarPathFinder.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/openset/BinaryHeapOpenSet.java
/// </summary>
public sealed class BinaryHeapOpenSet : IOpenSet
{
    /// <summary>
    /// The initial capacity of the heap (2^10)
    /// </summary>
    private const int InitialCapacity = 1024;

    /// <summary>
    /// The array backing the heap
    /// </summary>
    private PathNode[] _array;

    /// <summary>
    /// The size of the heap
    /// </summary>
    private int _size;

    public BinaryHeapOpenSet()
    {
        _size = 0;
        _array = new PathNode[InitialCapacity];
    }

    public BinaryHeapOpenSet(int size)
    {
        _size = 0;
        _array = new PathNode[size];
    }

    public int Size() => _size;

    public void Insert(PathNode value)
    {
        if (_size >= _array.Length - 1)
        {
            Array.Resize(ref _array, _array.Length << 1);
        }
        _size++;
        value.HeapPosition = _size;
        _array[_size] = value;
        Update(value);
    }

    public void Update(PathNode val)
    {
        int index = val.HeapPosition;
        int parentInd = index >> 1; // Right shift (C# doesn't have unsigned right shift, but >> works the same for positive numbers)
        double cost = val.CombinedCost;
        PathNode parentNode = _array[parentInd];
        while (index > 1 && parentNode.CombinedCost > cost)
        {
            _array[index] = parentNode;
            _array[parentInd] = val;
            val.HeapPosition = parentInd;
            parentNode.HeapPosition = index;
            index = parentInd;
            parentInd = index >> 1;
            parentNode = _array[parentInd];
        }
    }

    public bool IsEmpty() => _size == 0;

    public PathNode RemoveLowest()
    {
        if (_size == 0)
        {
            throw new InvalidOperationException("Cannot remove from empty heap");
        }
        PathNode result = _array[1];
        PathNode val = _array[_size];
        _array[1] = val;
        val.HeapPosition = 1;
        _array[_size] = null!;
        _size--;
        result.HeapPosition = -1;
        if (_size < 2)
        {
            return result;
        }
        int index = 1;
        int smallerChild = 2;
        double cost = val.CombinedCost;
        do
        {
            PathNode smallerChildNode = _array[smallerChild];
            double smallerChildCost = smallerChildNode.CombinedCost;
            if (smallerChild < _size)
            {
                PathNode rightChildNode = _array[smallerChild + 1];
                double rightChildCost = rightChildNode.CombinedCost;
                if (smallerChildCost > rightChildCost)
                {
                    smallerChild++;
                    smallerChildCost = rightChildCost;
                    smallerChildNode = rightChildNode;
                }
            }
            if (cost <= smallerChildCost)
            {
                break;
            }
            _array[index] = smallerChildNode;
            _array[smallerChild] = val;
            val.HeapPosition = smallerChild;
            smallerChildNode.HeapPosition = index;
            index = smallerChild;
        } while ((smallerChild <<= 1) <= _size);
        return result;
    }
}

