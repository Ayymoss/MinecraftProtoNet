using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Status.Clientbound;

public class StatusResponsePacket : Packet
{
    public override int PacketId => 0x00;
    public override PacketDirection Direction => PacketDirection.Clientbound;
    public string Response { get; set; } = string.Empty;

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Response = buffer.ReadString();
    }
}
