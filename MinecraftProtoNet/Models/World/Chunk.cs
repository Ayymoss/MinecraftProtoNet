using MinecraftProtoNet.Models.World.Meta;

namespace MinecraftProtoNet.Models.World;

public class Chunk(int x, int z, ChunkData chunkData, LightData lightData)
{
    public int X { get; set; } = x;
    public int Z { get; set; } = z;
    public ChunkData ChunkData { get; set; } = chunkData;
    public LightData LightData { get; set; } = lightData;
}
