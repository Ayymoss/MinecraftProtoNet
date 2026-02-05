using MinecraftProtoNet.Core.State.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Models.World.Chunk;

public class ChunkSection
{
    public const int Size = 16;

    private PalettedContainer BlockStates { get; set; } = new(PaletteType.BlockState);
    private PalettedContainer Biomes { get; set; } = new(PaletteType.Biome);

    public bool IsEmpty => NonEmptyBlockCount == 0;
    public short NonEmptyBlockCount { get; private set; }

    public BlockState? GetBlockStateId(int x, int y, int z)
    {
        var blockStateId = BlockStates.Get(GetBlockIndex(x, y, z));
        return !blockStateId.HasValue ? null : ClientState.BlockStateRegistry[blockStateId.Value];
    }

    private static int GetBlockIndex(int x, int y, int z) => (y << 8) | (z << 4) | x;
    private static int GetBiomeIndex(int x, int y, int z) => (y << 4) | (z << 2) | x;

    public void Read(ref PacketBufferReader reader)
    {
        NonEmptyBlockCount = reader.ReadSignedShort();
        BlockStates.Read(ref reader);
        Biomes.Read(ref reader);
    }

    public void SetBlockStateId(int x, int y, int z, int blockStateId)
    {
        var index = GetBlockIndex(x, y, z);
        BlockStates.Set(index, blockStateId);
        var isAir = ClientState.BlockStateRegistry[blockStateId].IsAir;

        switch (isAir)
        {
            case true when BlockStates.Get(index) is not 0:
                NonEmptyBlockCount--;
                break;
            case false when BlockStates.Get(index) is 0:
                NonEmptyBlockCount++;
                break;
        }
    }
}
