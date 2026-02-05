using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Status.Clientbound;

[Packet(0x00, ProtocolState.Status)]
public class StatusResponsePacket : IClientboundPacket
{
    public string Response { get; set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Response = buffer.ReadString();
    }
}
