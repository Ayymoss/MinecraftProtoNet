using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

[Packet(0x03, ProtocolState.Configuration)]
public class FinishConfigurationPacket : IClientPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // No fields to deserialize
    }
}
