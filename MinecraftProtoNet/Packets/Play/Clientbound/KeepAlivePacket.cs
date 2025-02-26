using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class KeepAlivePacket : Packet
{
    public override int PacketId => 0x27;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public long Payload { get; set; }
    
    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = buffer.ReadSignedLong();
    }
}
