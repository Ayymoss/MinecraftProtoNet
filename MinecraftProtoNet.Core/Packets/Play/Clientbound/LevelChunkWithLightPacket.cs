using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x2C, ProtocolState.Play, true)]
public class LevelChunkWithLightPacket : IClientboundPacket
{
    public int ChunkX { get; set; }
    public int ChunkZ { get; set; }
    public required Chunk Chunk { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ChunkX = buffer.ReadSignedInt();
        ChunkZ = buffer.ReadSignedInt();

        // Heightmaps: Map of Types (VarInt ID) to long[] (VarInt count)
        var heightmapCount = buffer.ReadVarInt();
        for (var i = 0; i < heightmapCount; i++)
        {
            var typeId = buffer.ReadVarInt();
            _ = buffer.ReadBitSet(); // Read long[] with VarInt length
        }

        // Chunk Data
        var chunkDataBuffer = buffer.ReadPrefixedArray<byte>();
        
        // Block Entities: List of info
        var blockEntitiesCount = buffer.ReadVarInt();
        for (var i = 0; i < blockEntitiesCount; i++)
        {
             _ = buffer.ReadChunkBlockEntity();
        }

        Chunk = new Chunk(ChunkX, ChunkZ);
        var chunkReader = new PacketBufferReader(chunkDataBuffer);
        Chunk.DeserializeSections(ref chunkReader);

        // Light Data
        var skyLightMask = buffer.ReadBitSet();
        var blockLightMask = buffer.ReadBitSet();
        var emptySkyLightMask = buffer.ReadBitSet();
        var emptyBlockLightMask = buffer.ReadBitSet();
        
        var skyUpdateCount = buffer.ReadVarInt();
        for (var i = 0; i < skyUpdateCount; i++)
        {
            _ = buffer.ReadPrefixedArray<byte>();
        }
        
        var blockUpdateCount = buffer.ReadVarInt();
        for (var i = 0; i < blockUpdateCount; i++)
        {
            _ = buffer.ReadPrefixedArray<byte>();
        }
    }
}
