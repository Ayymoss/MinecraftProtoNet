﻿using MinecraftProtoNet.NBT.Enums;

namespace MinecraftProtoNet.NBT.Tags.Primitive;

public class NbtDouble(string? name, double value) : NbtTag(name)
{
    public double Value { get; set; } = value;
    public override NbtTagType Type => NbtTagType.Double;
}
