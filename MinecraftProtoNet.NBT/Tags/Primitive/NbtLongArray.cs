using MinecraftProtoNet.NBT.Enums;

namespace MinecraftProtoNet.NBT.Tags.Primitive;

public class NbtLongArray(string? name, long[] value) : NbtTag(name)
{
    public long[] Value { get; set; } = value;
    public override NbtTagType Type => NbtTagType.LongArray;
}
