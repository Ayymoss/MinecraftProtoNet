using MinecraftProtoNet.Core.Models.World.Meta;
using MinecraftProtoNet.Core.NBT.Tags;

namespace MinecraftProtoNet.Core.Packets.Base.Definitions;

public class ChunkData(NbtTag heightmaps, byte[] data, ChunkBlockEntityInfo[] blockEntities)
{
    public NbtTag Heightmaps { get; set; } = heightmaps;
    public byte[] Data { get; set; } = data;
    public ChunkBlockEntityInfo[] BlockEntities { get; set; } = blockEntities;
}
