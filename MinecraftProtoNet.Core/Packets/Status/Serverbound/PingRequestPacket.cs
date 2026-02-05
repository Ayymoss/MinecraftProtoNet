using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Status.Serverbound;

[Packet(0x01, ProtocolState.Status)]
public class PingRequestPacket : IServerboundPacket
{
    public required long Payload { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteSignedLong(Payload);
    }
}
