using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Status.Clientbound;

[Packet(0x00, ProtocolState.Status)]
public class StatusResponsePacket : IClientboundPacket
{
    public string Response { get; set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Response = buffer.ReadString();
    }
}
