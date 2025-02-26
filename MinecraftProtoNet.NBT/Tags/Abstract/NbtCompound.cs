using MinecraftProtoNet.NBT.Enums;

namespace MinecraftProtoNet.NBT.Tags.Abstract;

public class NbtCompound(string? name) : NbtTag(name)
{
    public override NbtTagType Type => NbtTagType.Compound;
    public List<NbtTag> Value { get; } = [];
}
