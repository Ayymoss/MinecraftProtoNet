using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x53, ProtocolState.Play)]
public class SectionBlocksUpdatePacket : IClientboundPacket
{
    public Vector3<float> SectionPosition { get; set; }
    public long[] Blocks { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        SectionPosition = buffer.ReadChunkCoordinatePosition();
        Blocks = buffer.ReadPrefixedArray<VarLong>().ToLongArray();
    }
}
