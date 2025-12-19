using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x2C, ProtocolState.Play, true)]
public class LevelChunkWithLightPacket : IClientboundPacket
{
    public int ChunkX { get; set; }
    public int ChunkZ { get; set; }
    public Chunk Chunk { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ChunkX = buffer.ReadSignedInt();
        ChunkZ = buffer.ReadSignedInt();

        // Heightmaps - This should be collected when/if needed - for now, throwing it away
        var heightMaps = buffer.ReadVarInt();
        for (var i = 0; i < heightMaps; i++)
        {
            var type = buffer.ReadVarInt();
            var heightMapData = buffer.ReadPrefixedArray<long>();
        }

        // Chunk Data
        var chunkDataBuffer = buffer.ReadPrefixedArray<byte>();
        var blockEntities = buffer.ReadPrefixedArray<ChunkBlockEntityInfo>();

        Chunk = new Chunk(ChunkX, ChunkZ);
        var chunkReader = new PacketBufferReader(chunkDataBuffer);
        Chunk.DeserializeSections(ref chunkReader);

        // Light Data
        var skyLightMask = buffer.ReadPrefixedArray<long>();
        var blockLightMask = buffer.ReadPrefixedArray<long>();
        var emptySkyLightMask = buffer.ReadPrefixedArray<long>();
        var emptyBlockLightMask = buffer.ReadPrefixedArray<long>();
        var skyLight = buffer.ReadPrefixedArray<byte[]>();
        var blockLight = buffer.ReadPrefixedArray<byte[]>();
    }
}
