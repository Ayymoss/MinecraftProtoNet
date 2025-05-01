namespace MinecraftProtoNet.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class HandlesPacketAttribute(Type packetType) : Attribute
{
    public Type PacketType => packetType ?? throw new ArgumentNullException(nameof(packetType));
}
