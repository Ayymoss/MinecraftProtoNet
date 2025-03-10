using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class SingleValuePalette<T>(Dictionary<int, T> registry) : IPalette<T>
{
    private T _value;
    private bool _hasValue = false;

    public int IdFor(T value)
    {
        if (!_hasValue)
        {
            _value = value;
            _hasValue = true;
        }
        else if (!_value.Equals(value))
        {
            throw new InvalidOperationException("Cannot add more than one value to SingleValuePalette");
        }

        return 0;
    }

    public T ValueFor(int id)
    {
        if (id != 0 || !_hasValue) throw new IndexOutOfRangeException($"Invalid palette id: {id} - Type: {typeof(T)}");

        return _value;
    }

    public void Read(ref PacketBufferReader reader)
    {
        var id = reader.ReadVarInt();
        if (!registry.TryGetValue(id, out var value))
        {
            throw new IndexOutOfRangeException($"Invalid registry id: {id} - Type: {typeof(T)}");
        }

        _value = value;
        _hasValue = true;
    }
}
