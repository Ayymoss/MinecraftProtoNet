using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class GlobalPalette : IPalette
{
    public int IdFor(int registryId)
    {
        return registryId;
    }

    public int RegistryIdFor(int paletteId)
    {
        return paletteId;
    }

    public void Read(ref PacketBufferReader reader)
    {
        // Global palette doesn't have entries to read
    }
}
