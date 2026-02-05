using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

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
