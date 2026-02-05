using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x53, ProtocolState.Play)]
public class SectionBlocksUpdatePacket : IClientboundPacket
{
    public required Vector3<float> SectionPosition { get; set; }
    public required long[] Blocks { get; set; } = [];

    public void Deserialize(ref PacketBufferReader buffer)
    {
        SectionPosition = buffer.ReadChunkCoordinatePosition();
        Blocks = buffer.ReadPrefixedArray<VarLong>().ToLongArray();
    }
}
