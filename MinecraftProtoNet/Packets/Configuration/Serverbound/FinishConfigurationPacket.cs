using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Serverbound;

[Packet(0x03, ProtocolState.Configuration)]
public class FinishConfigurationPacket : IServerboundPacket
{
    public void Serialize(ref PacketBufferWriter buffer)
    {
    }
}
