using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x27, ProtocolState.Play)]
public class KeepAlivePacket : IClientPacket
{
    public long Payload { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = buffer.ReadSignedLong();
    }
}
