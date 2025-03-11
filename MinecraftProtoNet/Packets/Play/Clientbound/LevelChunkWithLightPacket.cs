using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x28, ProtocolState.Play)]
public class LevelChunkWithLightPacket : IClientPacket
{
    public int ChunkX { get; set; }
    public int ChunkZ { get; set; }
    public Chunk Chunk { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ChunkX = buffer.ReadSignedInt();
        ChunkZ = buffer.ReadSignedInt();

        // Chunk Data
        var heightmaps = buffer.ReadNbtTag();
        var chunkDataBuffer = buffer.ReadBuffer(buffer.ReadVarInt());
        var blockEntities = buffer.ReadPrefixedArray<ChunkBlockEntityInfo>();

        // Create the chunk
        Chunk = new Chunk(ChunkX, ChunkZ);

        // Parse chunk sections from the chunk data buffer
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
