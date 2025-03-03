using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.World;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.NBT.Tags.Abstract;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x28, ProtocolState.Play)]
public class LevelChunkWithLightPacket : IClientPacket
{
    public Chunk Chunk { get; set; } = null!; // This should never be null at call site.

    public void Deserialize(ref PacketBufferReader buffer)
    {
        var chunkX = buffer.ReadSignedInt();
        var chunkZ = buffer.ReadSignedInt();

        // Chunk Data
        var heightmaps = buffer.ReadNbtTag() ?? new NbtEnd();
        var chunkData = buffer.ReadBuffer(buffer.ReadVarInt());
        var blockEntities = buffer.ReadPrefixedArray<ChunkBlockEntity>();
        var chunkDataResult = new ChunkData(heightmaps, chunkData.ToArray(), blockEntities);

        // Light Data
        var skyLightMask = buffer.ReadPrefixedArray<long>();
        var blockLightMask = buffer.ReadPrefixedArray<long>();
        var emptySkyLightMask = buffer.ReadPrefixedArray<long>();
        var emptyBlockLightMask = buffer.ReadPrefixedArray<long>();
        var skyLight = buffer.ReadPrefixedArray<byte[]>();
        var blockLight = buffer.ReadPrefixedArray<byte[]>();
        var lightDataResult = new LightData(skyLightMask, blockLightMask, emptySkyLightMask, emptyBlockLightMask, skyLight, blockLight);

        Chunk = new Chunk(chunkX, chunkZ, chunkDataResult, lightDataResult);
    }
}
