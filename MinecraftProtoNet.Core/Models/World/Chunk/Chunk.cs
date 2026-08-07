using MinecraftProtoNet.Core.State.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Models.World.Chunk;

public class Chunk(int x, int z)
{
    public const int Width = 16;
    public const int SectionHeight = 16;

    public int X { get; private set; } = x;
    public int Z { get; private set; } = z;
    public ChunkSection[] Sections { get; private set; } = [];

    /// <summary>
    /// Index of the lowest chunk section, i.e. <c>dimension.minY >> 4</c>. Chunk data on the wire is just a
    /// run of sections from the bottom of the world upwards, with no Y stamped on them, so this is what maps a
    /// block Y onto a section — and it is a property of the DIMENSION, not a constant.
    ///
    /// It defaults to the vanilla overworld (-64), which is why a wrong value goes unnoticed on ordinary
    /// servers. In a dimension that starts at Y=0 every lookup is displaced by 64 blocks: the world reads as
    /// air where the ground is, so the player falls forever and the server rubber-bands them back.
    ///
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/level/dimension/DimensionType.java (minY)
    /// and net/minecraft/world/level/chunk/ChunkAccess.java (getMinSectionY).
    ///
    /// Static because chunk sections are decoded inside packet deserialisation, which has no client context.
    /// Set it via <see cref="SetWorldMinY"/> whenever a level loads, BEFORE its chunks arrive.
    /// </summary>
    public static int MinSection { get; private set; } = -4;

    /// <summary>
    /// Number of sections the world is tall (<c>dimension.height / 16</c>), used to size the section array.
    /// Defaults to the vanilla overworld's 24 (384 blocks).
    /// </summary>
    public static int SectionCount { get; private set; } = 24;

    /// <summary>
    /// Applies the current dimension's vertical bounds. Call on join and on respawn/dimension change, before
    /// that level's chunks are decoded.
    /// </summary>
    public static void SetWorldBounds(int minY, int height)
    {
        MinSection = minY >> 4;
        SectionCount = Math.Max(1, (height + SectionHeight - 1) / SectionHeight);
    }

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
        // A trailing section can be absent when the server sends fewer than the dimension's full height.
        if (section is null) return ClientState.BlockStateRegistry[0];
        return section.IsEmpty 
            // Empty sections are Air
            ? ClientState.BlockStateRegistry[0] 
            // Assuming 0 is Air
            : section.GetBlockStateId(localX, localY, localZ);
    }

    /// <summary>
    /// Checks if this chunk is empty (contains only air blocks).
    /// Equivalent to Java's LevelChunk.isEmpty().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:116
    /// Used by Baritone for chunk validation.
    /// </summary>
    public bool IsEmpty()
    {
        // Chunk is empty if all sections are empty or missing
        if (Sections.Length == 0) return true;

        foreach (var section in Sections)
        {
            if (section != null && !section.IsEmpty)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a chunk section by its Y coordinate (section index, not block Y).
    /// Equivalent to Java's LevelChunk.getSection(int sectionY).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:154+
    /// Used by Baritone for chunk section access.
    /// </summary>
    /// <param name="sectionY">The section Y coordinate (block Y >> 4).</param>
    /// <returns>The chunk section at the specified Y coordinate, or null if not present.</returns>
    public ChunkSection? GetSection(int sectionY)
    {
        var sectionIndex = GetSectionIndex(sectionY);
        if (sectionIndex < 0 || sectionIndex >= Sections.Length) return null;
        return Sections[sectionIndex];
    }

    private static int GetSectionIndex(int sectionY) => sectionY - MinSection;

    public void DeserializeSections(ref PacketBufferReader reader)
    {
        var sectionList = new ChunkSection[SectionCount];

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
