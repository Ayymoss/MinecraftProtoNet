using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class GlobalPalette<T>(Dictionary<int, T> registry) : IPalette<T>
{
    public int IdFor(T value)
    {
        foreach (var entry in registry)
        {
            if (entry.Value.Equals(value))
                return entry.Key;
        }

        return 0;
    }

    public T ValueFor(int id)
    {
        if (!registry.TryGetValue(id, out var value))
            throw new IndexOutOfRangeException($"Invalid registry id: {id} - Type: {typeof(T)}");

        return value;
    }

    public void Read(ref PacketBufferReader reader)
    {
        // Global palette doesn't have entries to read
    }
}
