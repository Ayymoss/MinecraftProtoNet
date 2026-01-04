using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Models.World.Chunk;

public interface IPalette
{
    int IdFor(int registryId);
    int RegistryIdFor(int paletteId);
    void Read(ref PacketBufferReader reader);
}
