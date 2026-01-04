using MinecraftProtoNet.Core.NBT.Enums;

namespace MinecraftProtoNet.Core.NBT.Tags.Abstract;

public class NbtEnd() : NbtTag(null)
{
    public override NbtTagType Type => NbtTagType.End;
}
