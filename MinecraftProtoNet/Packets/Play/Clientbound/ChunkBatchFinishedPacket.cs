using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Marks the end of a chunk batch. Contains the size of the batch.
/// </summary>
[Packet(0x0B, ProtocolState.Play)]
public class ChunkBatchFinishedPacket : IClientboundPacket
{
    public int BatchSize { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        BatchSize = buffer.ReadVarInt();
    }
}
