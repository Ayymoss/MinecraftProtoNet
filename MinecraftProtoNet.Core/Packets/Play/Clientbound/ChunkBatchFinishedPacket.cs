using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Marks the end of a chunk batch. Contains the size of the batch.
/// </summary>
[Packet(0x0B, ProtocolState.Play, true)]
public class ChunkBatchFinishedPacket : IClientboundPacket
{
    public int BatchSize { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        BatchSize = buffer.ReadVarInt();
    }
}
