using MinecraftProtoNet.Core.NBT.Tags;

namespace MinecraftProtoNet.Core.Models.World.Meta;

public class ChunkBlockEntityInfo(byte x, short y, byte z, int type, NbtTag nbt)
{
    public byte X { get; } = x;
    public short Y { get; } = y;
    public byte Z { get; } = z;
    public int Type { get; } = type;
    public NbtTag Nbt { get; } = nbt;
}
