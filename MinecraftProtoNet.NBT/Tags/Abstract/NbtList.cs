using MinecraftProtoNet.NBT.Enums;

namespace MinecraftProtoNet.NBT.Tags.Abstract;

public class NbtList(string? name, NbtTagType listType) : NbtTag(name)
{
    public List<NbtTag> Value { get; } = [];
    public NbtTagType ListType { get; } = listType;
    public override NbtTagType Type => NbtTagType.List;
}
