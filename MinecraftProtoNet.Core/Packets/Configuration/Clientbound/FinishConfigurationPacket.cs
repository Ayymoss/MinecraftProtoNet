using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Clientbound;

[Packet(0x03, ProtocolState.Configuration)]
public class FinishConfigurationPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // No fields to deserialize
    }
}
