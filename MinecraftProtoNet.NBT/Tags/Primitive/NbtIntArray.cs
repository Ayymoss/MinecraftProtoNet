using MinecraftProtoNet.NBT.Enums;

namespace MinecraftProtoNet.NBT.Tags.Primitive;

public class NbtIntArray(string? name, int[] value) : NbtTag(name)
{
    public int[] Value { get; set; } = value;
    public override NbtTagType Type => NbtTagType.IntArray;
}
