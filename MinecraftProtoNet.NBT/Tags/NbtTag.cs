using MinecraftProtoNet.NBT.Enums;

namespace MinecraftProtoNet.NBT.Tags;

public abstract class NbtTag(string? name)
{
    public string? Name { get; set; } = name;
    public abstract NbtTagType Type { get; }

    public override string ToString()
    {
        return $"{nameof(NbtTag)}: {Name ?? "<NULL>"} ({Type})";
    }
}
