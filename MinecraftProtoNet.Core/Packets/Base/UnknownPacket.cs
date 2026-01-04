using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Base;

[Packet(-0x01, ProtocolState.Undefined)]
public class UnknownPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
    }
}
