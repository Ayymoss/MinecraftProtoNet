using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Status.Serverbound;

public class PingRequestPacket : Packet
{
    public override int PacketId => 0x01;
    public override PacketDirection Direction => PacketDirection.Serverbound;
    public required long Payload { get; set; }

    public override void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(PacketId);
        var payloadBytes = BitConverter.GetBytes(Payload);
        if (BitConverter.IsLittleEndian) Array.Reverse(payloadBytes);
        buffer.WriteBuffer(payloadBytes);
    }
}
