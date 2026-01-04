using MinecraftProtoNet.Core.Core;

namespace MinecraftProtoNet.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class PacketAttribute(int id, ProtocolState state, bool silent = false) : Attribute
{
    public int PacketId => id;
    public ProtocolState ProtocolState { get; set; } = state;
    public bool Silent { get; set; } = silent;
}
