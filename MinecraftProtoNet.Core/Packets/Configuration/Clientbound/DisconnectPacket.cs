using System.Text;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Clientbound;

[Packet(0x02, ProtocolState.Configuration)]
public class DisconnectPacket : IClientboundPacket
{
    public required string Reason { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Reason = Encoding.UTF8.GetString(buffer.ReadRestBuffer());
    }
}
