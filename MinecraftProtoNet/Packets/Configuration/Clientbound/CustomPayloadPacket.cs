using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

public class CustomPayloadPacket : Packet
{
    public override int PacketId => 0x01;
    public override PacketDirection Direction => PacketDirection.Clientbound;
    public string Channel { get; set; }
    public byte[] Data { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Channel = buffer.ReadString();
        Data = buffer.ReadRestBuffer().ToArray();
    }
}
