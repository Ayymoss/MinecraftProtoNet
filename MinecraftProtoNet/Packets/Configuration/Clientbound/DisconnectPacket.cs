using System.Text;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

[Packet(0x02, ProtocolState.Configuration)]
public class DisconnectPacket : IClientboundPacket
{
    public required string Reason { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Reason = Encoding.UTF8.GetString(buffer.ReadRestBuffer());
    }
}
