using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

/// <summary>
/// Sent by the client after receiving a ChunkBatchFinished packet to acknowledge receipt
/// and inform the server about desired chunk loading rate.
/// </summary>
[Packet(0x0A, ProtocolState.Play, true)]
public class ChunkBatchReceivedPacket : IServerboundPacket
{
    /// <summary>
    /// Desired number of chunks per tick. Server uses this to throttle chunk sending.
    /// A reasonable default is 7.0 (similar to vanilla client).
    /// </summary>
    public required float DesiredChunksPerTick { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteFloat(DesiredChunksPerTick);
    }
}
