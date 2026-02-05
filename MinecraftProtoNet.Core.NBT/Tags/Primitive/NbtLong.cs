using MinecraftProtoNet.Core.NBT.Enums;

namespace MinecraftProtoNet.Core.NBT.Tags.Primitive;

public class NbtLong(string? name, long value) : NbtTag(name)
{
    public long Value { get; set; } = value;
    public override NbtTagType Type => NbtTagType.Long;
}
