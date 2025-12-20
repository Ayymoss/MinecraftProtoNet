using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Marks the start of a chunk batch. This packet has no data.
/// </summary>
[Packet(0x0C, ProtocolState.Play, true)]
public class ChunkBatchStartPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // Empty packet - no data to read
    }
}
