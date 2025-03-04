using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Serverbound;

[Packet(0x04, ProtocolState.Configuration)]
public class KeepAlivePacket : IServerPacket
{
    public required long Payload { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(this.GetPacketAttributeValue(p => p.PacketId));
        buffer.WriteSignedLong(Payload);
    }
}
