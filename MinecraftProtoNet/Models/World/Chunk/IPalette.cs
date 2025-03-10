using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public interface IPalette<T>
{
    int IdFor(T value);
    T ValueFor(int id);
    void Read(ref PacketBufferReader reader);
}
