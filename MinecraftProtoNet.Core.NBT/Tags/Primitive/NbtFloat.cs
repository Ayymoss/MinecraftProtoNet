using MinecraftProtoNet.Core.NBT.Enums;

namespace MinecraftProtoNet.Core.NBT.Tags.Primitive;

public class NbtFloat(string? name, float value) : NbtTag(name)
{
    public float Value { get; set; } = value;
    public override NbtTagType Type => NbtTagType.Float;
}
