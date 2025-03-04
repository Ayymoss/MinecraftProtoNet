using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Status.Serverbound;

[Packet(0x00, ProtocolState.Status)]
public class StatusRequestPacket : IServerPacket
{
    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(this.GetPacketAttributeValue(p => p.PacketId));
    }
}
