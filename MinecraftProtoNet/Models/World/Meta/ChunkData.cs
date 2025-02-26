using MinecraftProtoNet.NBT.Tags;

namespace MinecraftProtoNet.Models.World.Meta;

public class ChunkData(NbtTag heightmaps, byte[] data, ChunkBlockEntity[] blockEntities)
{
    public NbtTag Heightmaps { get; set; } = heightmaps;
    public byte[] Data { get; set; } = data;
    public ChunkBlockEntity[] BlockEntities { get; set; } = blockEntities;
}
