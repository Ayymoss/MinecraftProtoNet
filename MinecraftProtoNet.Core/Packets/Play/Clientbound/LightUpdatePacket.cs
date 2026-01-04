using System.Collections;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Updates light data for a chunk.
/// </summary>
[Packet(0x2F, ProtocolState.Play)]
public class LightUpdatePacket : IClientboundPacket
{
    public int ChunkX { get; set; }
    public int ChunkZ { get; set; }

    // BitSets indicating which sections have light data
    public long[] SkyYMask { get; set; } = [];
    public long[] BlockYMask { get; set; } = [];
    public long[] EmptySkyYMask { get; set; } = [];
    public long[] EmptyBlockYMask { get; set; } = [];

    // Light data arrays (each array is 2048 bytes)
    public List<byte[]> SkyUpdates { get; set; } = [];
    public List<byte[]> BlockUpdates { get; set; } = [];

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ChunkX = buffer.ReadVarInt();
        ChunkZ = buffer.ReadVarInt();

        // Read BitSets
        SkyYMask = buffer.ReadBitSet();
        BlockYMask = buffer.ReadBitSet();
        EmptySkyYMask = buffer.ReadBitSet();
        EmptyBlockYMask = buffer.ReadBitSet();

        // Read sky updates (list of 2048-byte arrays)
        var skyUpdatesCount = buffer.ReadVarInt();
        SkyUpdates = new List<byte[]>(skyUpdatesCount);
        for (var i = 0; i < skyUpdatesCount; i++)
        {
            var length = buffer.ReadVarInt();
            SkyUpdates.Add(buffer.ReadBytes(length).ToArray());
        }

        // Read block updates (list of 2048-byte arrays)
        var blockUpdatesCount = buffer.ReadVarInt();
        BlockUpdates = new List<byte[]>(blockUpdatesCount);
        for (var i = 0; i < blockUpdatesCount; i++)
        {
            var length = buffer.ReadVarInt();
            BlockUpdates.Add(buffer.ReadBytes(length).ToArray());
        }
    }
}
