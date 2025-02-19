using System.Text;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

public class DisconnectPacket : Packet
{
    public override int PacketId => 0x02;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public string Reason { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        // TODO: Support full NBT deserialization
        Reason = Encoding.UTF8.GetString(buffer.ReadRestBuffer());
    }
}
