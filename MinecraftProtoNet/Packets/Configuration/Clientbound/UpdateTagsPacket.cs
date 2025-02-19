using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

public class UpdateTagsPacket : Packet
{
    public override int PacketId => 0x0D;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        // TODO
    }
}
