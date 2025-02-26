using MinecraftProtoNet.NBT.Enums;

namespace MinecraftProtoNet.NBT.Tags.Primitive;

public class NbtString(string? name, string value) : NbtTag(name)
{
    public string Value { get; set; } = value;
    public override NbtTagType Type => NbtTagType.String;
}
