using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class Chunk
{
    public const int Width = 16;
    public const int SectionHeight = 16;

    public int X { get; private set; }
    public int Z { get; private set; }

    public NbtTag Heightmaps { get; private set; }
    public ChunkBlockEntityInfo[] BlockEntities { get; private set; }
    public ChunkSection[] Sections { get; private set; }

    private const int MinSection = -4; // Default for 1.18+ (Y=-64)
    private const int MaxSection = 19; // Default for 1.18+ (Y=319)

    public Chunk(int x, int z, NbtTag heightmaps, ChunkBlockEntityInfo[] blockEntities)
    {
        X = x;
        Z = z;
        Heightmaps = heightmaps;
        BlockEntities = blockEntities;
    }

    public BlockState? GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= Width || z < 0 || z >= Width)
            throw new ArgumentOutOfRangeException($"Block position ({x}, {y}, {z}) is outside chunk boundaries");

        var sectionY = y >> 4;
        var sectionIndex = GetSectionIndex(sectionY);

        if (sectionIndex < 0 || sectionIndex >= Sections.Length) return null;

        var section = Sections[sectionIndex];
        if (section.IsEmpty) return null;

        var localX = x & 0xF;
        var localY = y & 0xF;
        var localZ = z & 0xF;

        return section.GetBlockState(localX, localY, localZ);
    }

    public Biome? GetBiome(int x, int y, int z)
    {
        if (x < 0 || x >= Width || z < 0 || z >= Width)
            throw new ArgumentOutOfRangeException($"Biome position ({x}, {y}, {z}) is outside chunk boundaries");

        var sectionY = y >> 4;
        var sectionIndex = GetSectionIndex(sectionY);

        if (sectionIndex < 0 || sectionIndex >= Sections.Length) return null;

        var section = Sections[sectionIndex];

        var localX = x & 0xF;
        var localY = y & 0xF;
        var localZ = z & 0xF;

        return section.GetBiome(localX, localY, localZ);
    }

    private static int GetSectionIndex(int sectionY) => sectionY - MinSection;

    public void DeserializeSections(ref PacketBufferReader reader)
    {
        List<ChunkSection> sectionList = [];
        const int expectedSections = MaxSection - MinSection + 1;

        for (var i = 0; i < expectedSections; i++)
        {
            if (reader.ReadableBytes <= 0) break;

            var section = new ChunkSection();
            section.Read(ref reader);
            sectionList.Add(section);

            if (reader.ReadableBytes <= 0) break;
        }

        Sections = sectionList.ToArray();
    }

    public int CalculateAbsoluteY(int sectionIndex, int localY)
    {
        return (sectionIndex + MinSection) * SectionHeight + localY;
    }

    public override string ToString()
    {
        return $"Sections: {Sections.Length}, BlockEntities: {BlockEntities.Length})";
    }
}
