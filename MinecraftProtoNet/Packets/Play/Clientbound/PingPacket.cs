using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x37, ProtocolState.Play)]
public class PingPacket : IClientPacket
{
    public int Id { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Id = buffer.ReadSignedInt();
    }
}
