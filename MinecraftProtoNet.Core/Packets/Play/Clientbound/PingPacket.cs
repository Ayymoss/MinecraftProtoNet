using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x3C, ProtocolState.Play)]
public class PingPacket : IClientboundPacket
{
    public int Id { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Id = buffer.ReadSignedInt();
    }
}
