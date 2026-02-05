using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Clientbound;

[Packet(0x04, ProtocolState.Configuration)]
public class KeepAlivePacket : IClientboundPacket
{
    public long Payload { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = buffer.ReadSignedLong();
    }
}
