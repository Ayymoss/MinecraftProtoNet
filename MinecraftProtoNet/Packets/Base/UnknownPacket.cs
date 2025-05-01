using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Base;

[Packet(-0x01, ProtocolState.Undefined)]
public class UnknownPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
    }
}
