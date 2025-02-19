using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Clientbound;

public class SetCompressionPacket : Packet
{
    public override int PacketId => 0x03;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public int Threshold { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Threshold = buffer.ReadVarInt();
    }
}
