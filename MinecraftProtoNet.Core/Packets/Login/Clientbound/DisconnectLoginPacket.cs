using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Login.Clientbound;

[Packet(0x00, ProtocolState.Login)]
public class DisconnectLoginPacket : IClientboundPacket
{
    public string Reason { get; set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Reason = buffer.ReadString();
    }
}
