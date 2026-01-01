using MinecraftProtoNet.State.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class Chunk(int x, int z)
{
    public const int Width = 16;
    public const int SectionHeight = 16;

    public int X { get; private set; } = x;
    public int Z { get; private set; } = z;
    public ChunkSection[] Sections { get; private set; } = [];

    private const int MinSection = -4; // Default for 1.18+ (Y=-64)
    private const int MaxSection = 19; // Default for 1.18+ (Y=319)

    public BlockState? GetBlock(int x, int y, int z)
    {
        var localX = x & 0xF;
        var localY = y & 0xF;
        var localZ = z & 0xF;

        if (localX < 0 || localX >= Width || localZ < 0 || localZ >= Width)
            throw new ArgumentOutOfRangeException($"Block position ({x}, {y}, {z}) is outside chunk boundaries");

        var sectionY = y >> 4;
        var sectionIndex = GetSectionIndex(sectionY);

        if (sectionIndex < 0 || sectionIndex >= Sections.Length) return null;

        var section = Sections[sectionIndex];
        return section.IsEmpty 
            // Empty sections are Air
            ? ClientState.BlockStateRegistry[0] 
            // Assuming 0 is Air
            : section.GetBlockStateId(localX, localY, localZ);
    }

    private static int GetSectionIndex(int sectionY) => sectionY - MinSection;

    public void DeserializeSections(ref PacketBufferReader reader)
    {
        var sectionList = new ChunkSection[MaxSection - MinSection + 1];

        for (var i = 0; i < sectionList.Length; i++)
        {
            if (reader.ReadableBytes <= 0) break;

            var section = new ChunkSection();
            section.Read(ref reader);
            sectionList[i] = section;
        }

        Sections = sectionList;
    }

    public void SetBlock(int x, int y, int z, int blockStateId)
    {
        var localX = x & 0xF;
        var localY = y & 0xF;
        var localZ = z & 0xF;

        var sectionY = y >> 4;
        var sectionIndex = GetSectionIndex(sectionY);

        if (sectionIndex < 0 || sectionIndex >= Sections.Length) return;

        Sections[sectionIndex].SetBlockStateId(localX, localY, localZ, blockStateId);
    }

    public override string ToString()
    {
        return $"{Sections.Length} sections";
    }
}
