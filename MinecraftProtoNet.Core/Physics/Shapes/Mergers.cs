using System.Collections;

namespace MinecraftProtoNet.Core.Physics.Shapes;

public static class MathHelpers
{
    public static int Gcd(int a, int b)
    {
        return b == 0 ? a : Gcd(b, a % b);
    }
    
    public static long Lcm(int a, int b)
    {
        if (a == 0 || b == 0) return 0;
        int gcd = Gcd(a, b);
        return Math.Abs((long)a * b) / gcd;
    }
}

public sealed class DiscreteCubeMerger : IIndexMerger
{
    private readonly CubePointRange _result;
    private readonly int _firstDiv;
    private readonly int _secondDiv;

    public DiscreteCubeMerger(int firstSize, int secondSize)
    {
        _result = new CubePointRange((int)MathHelpers.Lcm(firstSize, secondSize));
        int gcd = MathHelpers.Gcd(firstSize, secondSize);
        _firstDiv = firstSize / gcd;
        _secondDiv = secondSize / gcd;
    }

    public bool ForMergedIndexes(IndexConsumer consumer)
    {
        int size = _result.Count - 1;
        for (int i = 0; i < size; ++i)
        {
            if (!consumer(i / _secondDiv, i / _firstDiv, i))
            {
                return false;
            }
        }
        return true;
    }

    public int Size() => _result.Count;

    public IList<double> GetList()
    {
        // IDoubleList is technically IEnumerable<double> but simpler locally to return List wrapper or implementing IList
        // Java getList returns DoubleList. IIndexMerger GetList returns IList<double>.
        // I should return a wrapper or use CubePointRange as list.
        // My IDoubleList interface is not IList<double>.
        // IIndexMerger interface defined: IList<double> GetList().
        // I should fix IIndexMerger to return IDoubleList or change implementation.
        // I'll update IIndexMerger in next step or use adaptation here.
        // For now I'll create a List from it, which is inefficient but safe.
        // Or better: update IIndexMerger definition to return IDoubleList.
        // But I can't update previous file easily without a separate call.
        // Ill assume I can return a custom IList wrapper or just materialize.
        // Materializing is fine for now.
        var list = new List<double>(_result.Count);
        for(int i=0; i<_result.Count; i++) list.Add(_result.GetDouble(i));
        return list;
    }
    
    public IDoubleList GetListAsIDoubleList() => _result;
}

public sealed class IdenticalMerger : IIndexMerger
{
    private readonly IDoubleList _coords;

    public IdenticalMerger(IDoubleList coords)
    {
        _coords = coords;
    }

    public bool ForMergedIndexes(IndexConsumer consumer)
    {
        int size = _coords.Count - 1;
        for (int i = 0; i < size; ++i)
        {
            if (!consumer(i, i, i))
            {
                return false;
            }
        }
        return true;
    }

    public int Size() => _coords.Count;

    public IList<double> GetList()
    {
        // Same issue, materializing
        var list = new List<double>(_coords.Count);
        foreach(var d in _coords) list.Add(d);
        return list;
    }
}

public sealed class IndirectMerger : IIndexMerger
{
    private static readonly IDoubleList Empty = new ArrayDoubleList(new[] { 0.0 });
    private readonly double[] _result;
    private readonly int[] _firstIndices;
    private readonly int[] _secondIndices;
    private readonly int _resultLength;

    public IndirectMerger(IDoubleList first, IDoubleList second, bool firstOnlyMatters, bool secondOnlyMatters)
    {
        double lastValue = double.NaN;
        int firstSize = first.Count;
        int secondSize = second.Count;
        int capacity = firstSize + secondSize;
        _result = new double[capacity];
        _firstIndices = new int[capacity];
        _secondIndices = new int[capacity];
        bool canSkipFirst = !firstOnlyMatters;
        bool canSkipSecond = !secondOnlyMatters;
        int resultIndex = 0;
        int firstIndex = 0;
        int secondIndex = 0;

        while (true)
        {
            bool ranOutOfFirst = firstIndex >= firstSize;
            bool ranOutOfSecond = secondIndex >= secondSize;
            if (ranOutOfFirst && ranOutOfSecond)
            {
                _resultLength = Math.Max(1, resultIndex);
                return;
            }

            bool choseFirst = !ranOutOfFirst && (ranOutOfSecond || first.GetDouble(firstIndex) < second.GetDouble(secondIndex) + 1.0E-7);
            if (choseFirst)
            {
                ++firstIndex;
                if (canSkipFirst && (secondIndex == 0 || ranOutOfSecond)) continue;
            }
            else
            {
                ++secondIndex;
                if (canSkipSecond && (firstIndex == 0 || ranOutOfFirst)) continue;
            }

            int currentFirstIndex = firstIndex - 1;
            int currentSecondIndex = secondIndex - 1;
            double nextValue = choseFirst ? first.GetDouble(currentFirstIndex) : second.GetDouble(currentSecondIndex);

            if (!(lastValue >= nextValue - 1.0E-7))
            {
                _firstIndices[resultIndex] = currentFirstIndex;
                _secondIndices[resultIndex] = currentSecondIndex;
                _result[resultIndex] = nextValue;
                ++resultIndex;
                lastValue = nextValue;
            }
            else
            {
                _firstIndices[resultIndex - 1] = currentFirstIndex;
                _secondIndices[resultIndex - 1] = currentSecondIndex;
            }
        }
    }

    public bool ForMergedIndexes(IndexConsumer consumer)
    {
        int length = _resultLength - 1;
        for (int i = 0; i < length; ++i)
        {
            if (!consumer(_firstIndices[i], _secondIndices[i], i))
            {
                return false;
            }
        }
        return true;
    }

    public int Size() => _resultLength;

    public IList<double> GetList()
    {
        if (_resultLength <= 1) return new List<double> { 0.0 };
        // Create slice
        var list = new List<double>(_resultLength);
        for(int i=0; i<_resultLength; i++) list.Add(_result[i]);
        return list;
    }
    
    public IDoubleList GetListAsIDoubleList()
    { 
         if (_resultLength <= 1) return Empty;
         // Return view
         double[] slice = new double[_resultLength];
         Array.Copy(_result, slice, _resultLength);
         return new ArrayDoubleList(slice);
    }
}

public sealed class NonOverlappingMerger : IIndexMerger, IDoubleList
{
    private readonly IDoubleList _lower;
    private readonly IDoubleList _upper;
    private readonly bool _swap;

    public NonOverlappingMerger(IDoubleList lower, IDoubleList upper, bool swap)
    {
        _lower = lower;
        _upper = upper;
        _swap = swap;
    }

    public int Size() => _lower.Count + _upper.Count;
    public int Count => Size();

    public bool ForMergedIndexes(IndexConsumer consumer)
    {
        if (_swap)
        {
            return ForNonSwappedIndexes((f, s, r) => consumer(s, f, r));
        }
        return ForNonSwappedIndexes(consumer);
    }

    private bool ForNonSwappedIndexes(IndexConsumer consumer)
    {
        int lowerSize = _lower.Count;
        for (int i = 0; i < lowerSize; ++i)
        {
            if (!consumer(i, -1, i)) return false;
        }

        int upperSize = _upper.Count - 1;
        for (int i = 0; i < upperSize; ++i)
        {
            if (!consumer(lowerSize - 1, i, lowerSize + i)) return false;
        }
        return true;
    }

    public double GetDouble(int index)
    {
        return index < _lower.Count ? _lower.GetDouble(index) : _upper.GetDouble(index - _lower.Count);
    }

    public IList<double> GetList()
    {
        var list = new List<double>(Count);
        foreach (var d in this) list.Add(d);
        return list;
    }
    
    public IEnumerator<double> GetEnumerator()
    {
        foreach (var d in _lower) yield return d;
        foreach (var d in _upper) yield return d;
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
