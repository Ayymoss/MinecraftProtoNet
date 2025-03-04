using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Serverbound;

[Packet(0x03, ProtocolState.Login)]
public class LoginAcknowledgedPacket : IServerPacket
{
    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(this.GetPacketAttributeValue(p => p.PacketId));
    }
}
