using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class ChunkSection
{
    public const int Size = 16;

    private PalettedContainer<BlockState> BlockStates { get; set; } = new(PaletteType.BlockState);
    private PalettedContainer<Biome> Biomes { get; set; } = new(PaletteType.Biome);

    public bool IsEmpty => NonEmptyBlockCount == 0;
    public short NonEmptyBlockCount { get; private set; }

    public BlockState GetBlockState(int x, int y, int z) => BlockStates.Get(GetBlockIndex(x, y, z));

    public Biome GetBiome(int x, int y, int z)
    {
        var biomeX = x >> 2; // x / 4
        var biomeY = y >> 2; // y / 4
        var biomeZ = z >> 2; // z / 4

        return Biomes.Get(GetBiomeIndex(biomeX, biomeY, biomeZ));
    }

    private int GetBlockIndex(int x, int y, int z) => (y << 8) | (z << 4) | x;
    private int GetBiomeIndex(int x, int y, int z) => (y << 4) | (z << 2) | x;

    public void Read(ref PacketBufferReader reader)
    {
        NonEmptyBlockCount = reader.ReadSignedShort();
        BlockStates.Read(ref reader);
        Biomes.Read(ref reader);
    }
}
