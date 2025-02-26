using MinecraftProtoNet.NBT.Enums;

namespace MinecraftProtoNet.NBT.Tags.Primitive;

public class NbtByteArray(string? name, byte[] value) : NbtTag(name)
{
    public byte[] Value { get; set; } = value;
    public override NbtTagType Type => NbtTagType.ByteArray;

}
