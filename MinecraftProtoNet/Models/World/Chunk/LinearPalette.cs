using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class LinearPalette<T>(Dictionary<int, T> registry, int bits) : IPalette<T>
{
    private readonly T[] _values = new T[1 << bits];
    private int _bits = bits;
    private int _size = 0;

    public int IdFor(T value)
    {
        for (var i = 0; i < _size; i++)
        {
            if (_values[i].Equals(value)) return i;
        }

        if (_size >= _values.Length) throw new InvalidOperationException("Palette is full");

        _values[_size] = value;
        return _size++;
    }

    public T ValueFor(int id)
    {
        if (id < 0 || id >= _size)
            throw new IndexOutOfRangeException($"Invalid palette id: {id}");

        return _values[id];
    }

    public void Read(ref PacketBufferReader reader)
    {
        _size = reader.ReadVarInt();// TODO: Validate with PrefixedArray helper.
        for (var i = 0; i < _size; i++)
        {
            var id = reader.ReadVarInt();
            if (!registry.TryGetValue(id, out var value))
            {
                throw new IndexOutOfRangeException($"Invalid registry id: {id} - Type: {typeof(T)}");
            }

            _values[i] = value;
        }
    }
}
