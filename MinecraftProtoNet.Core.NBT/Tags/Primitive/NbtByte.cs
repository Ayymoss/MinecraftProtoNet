using MinecraftProtoNet.Core.NBT.Enums;

namespace MinecraftProtoNet.Core.NBT.Tags.Primitive;

public class NbtByte(string? name, byte value) : NbtTag(name)
{
    public byte Value { get; set; } = value;
    public override NbtTagType Type => NbtTagType.Byte;
}
