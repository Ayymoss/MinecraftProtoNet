using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Serverbound;

[Packet(0x03, ProtocolState.Configuration)]
public class FinishConfigurationPacket : IServerboundPacket
{
    public void Serialize(ref PacketBufferWriter buffer)
    {
    }
}
