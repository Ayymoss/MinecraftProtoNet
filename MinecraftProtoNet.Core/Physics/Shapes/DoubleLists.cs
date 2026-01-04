using System;
using System.Collections;
using System.Collections.Generic;

namespace MinecraftProtoNet.Physics.Shapes;

public interface IDoubleList : IEnumerable<double>
{
    double GetDouble(int index);
    int Count { get; }
}

public class CubePointRange : IDoubleList
{
    private readonly int _parts;

    public CubePointRange(int parts)
    {
        if (parts <= 0)
            throw new ArgumentException("Need at least 1 part");
        _parts = parts;
    }

    public double GetDouble(int index) => (double)index / _parts;
    public int Count => _parts + 1;

    public IEnumerator<double> GetEnumerator()
    {
        for (int i = 0; i < Count; i++) yield return GetDouble(i);
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class OffsetDoubleList : IDoubleList
{
    private readonly IDoubleList _delegate;
    private readonly double _offset;

    public OffsetDoubleList(IDoubleList list, double offset)
    {
        _delegate = list;
        _offset = offset;
    }

    public double GetDouble(int index) => _delegate.GetDouble(index) + _offset;
    public int Count => _delegate.Count;
    
    public IEnumerator<double> GetEnumerator()
    {
        for (int i = 0; i < Count; i++) yield return GetDouble(i);
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class ArrayDoubleList : IDoubleList
{
    private readonly double[] _array;

    public ArrayDoubleList(double[] array)
    {
        _array = array;
    }

    public double GetDouble(int index) => _array[index];
    public int Count => _array.Length;
    
    public IEnumerator<double> GetEnumerator()
    {
        foreach (var d in _array) yield return d;
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
