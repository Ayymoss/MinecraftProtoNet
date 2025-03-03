using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Status.Serverbound;

[Packet(0x01, ProtocolState.Status)]
public class PingRequestPacket : IServerPacket
{
    public required long Payload { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(this.GetPacketId());

        var payloadBytes = BitConverter.GetBytes(Payload);
        if (BitConverter.IsLittleEndian) Array.Reverse(payloadBytes);
        buffer.WriteBuffer(payloadBytes);
    }
}
