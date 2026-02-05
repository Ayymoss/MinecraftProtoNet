using MinecraftProtoNet.Core.NBT.Enums;

namespace MinecraftProtoNet.Core.NBT.Tags.Primitive;

public class NbtShort(string? name, short value) : NbtTag(name)
{
    public short Value { get; set; } = value;
    public override NbtTagType Type => NbtTagType.Short;
}
