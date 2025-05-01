using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Clientbound;

[Packet(0x00, ProtocolState.Login)]
public class DisconnectLoginPacket : IClientboundPacket
{
    public string Reason { get; set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Reason = buffer.ReadString();
    }
}
