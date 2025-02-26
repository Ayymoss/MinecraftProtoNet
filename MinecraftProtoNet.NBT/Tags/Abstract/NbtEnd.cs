using MinecraftProtoNet.NBT.Enums;

namespace MinecraftProtoNet.NBT.Tags.Abstract;

public class NbtEnd() : NbtTag(null)
{
    public override NbtTagType Type => NbtTagType.End;
}
