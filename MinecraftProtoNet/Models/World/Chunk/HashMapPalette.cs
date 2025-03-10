using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class HashMapPalette<T> : IPalette<T>
{
    private readonly Dictionary<int, T> _registry;
    private readonly Dictionary<T, int> _idMap = new();
    private readonly List<T> _values = [];

    public HashMapPalette(Dictionary<int, T> registry, int bits)
    {
        _registry = registry;
    }

    public int IdFor(T value)
    {
        if (_idMap.TryGetValue(value, out var id)) return id;

        id = _values.Count;
        _values.Add(value);
        _idMap[value] = id;
        return id;
    }

    public T ValueFor(int id)
    {
        if (id < 0 || id >= _values.Count)
            throw new IndexOutOfRangeException($"Invalid palette id: {id} - Type: {typeof(T)}");

        return _values[id];
    }

    public void Read(ref PacketBufferReader reader)
    {
        var size = reader.ReadVarInt();// TODO: Validate with PrefixedArray helper.
        _values.Clear();
        _idMap.Clear();

        for (var i = 0; i < size; i++)
        {
            var id = reader.ReadVarInt();
            if (!_registry.TryGetValue(id, out var value))
            {
                throw new IndexOutOfRangeException($"Invalid registry id: {id} - Type: {typeof(T)}");
            }

            _values.Add(value);
            _idMap[value] = i;
        }
    }
}
