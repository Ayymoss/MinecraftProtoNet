using MinecraftProtoNet.Core.NBT.Enums;

namespace MinecraftProtoNet.Core.NBT.Tags.Abstract;

public class NbtCompound(string? name) : NbtTag(name)
{
    public override NbtTagType Type => NbtTagType.Compound;
    public List<NbtTag> Value { get; } = [];
}
