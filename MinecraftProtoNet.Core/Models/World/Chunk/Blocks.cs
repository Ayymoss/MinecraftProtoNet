using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Models.World.Chunk;

/// <summary>
/// Provides block constants matching Java's net.minecraft.world.level.block.Blocks.
/// Equivalent to Java's Blocks class constants.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:56
/// Used by Baritone for block comparison and identification.
/// </summary>
public static class Blocks
{
    /// <summary>
    /// Gets the air block state (ID 0).
    /// Equivalent to Java's Blocks.AIR.defaultBlockState().
    /// </summary>
    public static BlockState? Air => ClientState.BlockStateRegistry.GetValueOrDefault(0);

    /// <summary>
    /// Gets the stone block state.
    /// Equivalent to Java's Blocks.STONE.defaultBlockState().
    /// </summary>
    public static BlockState? Stone => GetBlockState("minecraft:stone");

    /// <summary>
    /// Gets the grass block state.
    /// Equivalent to Java's Blocks.GRASS_BLOCK.defaultBlockState().
    /// </summary>
    public static BlockState? GrassBlock => GetBlockState("minecraft:grass_block");

    /// <summary>
    /// Gets the dirt block state.
    /// Equivalent to Java's Blocks.DIRT.defaultBlockState().
    /// </summary>
    public static BlockState? Dirt => GetBlockState("minecraft:dirt");

    /// <summary>
    /// Gets the cobblestone block state.
    /// Equivalent to Java's Blocks.COBBLESTONE.defaultBlockState().
    /// </summary>
    public static BlockState? Cobblestone => GetBlockState("minecraft:cobblestone");

    /// <summary>
    /// Gets the bedrock block state.
    /// Equivalent to Java's Blocks.BEDROCK.defaultBlockState().
    /// </summary>
    public static BlockState? Bedrock => GetBlockState("minecraft:bedrock");

    /// <summary>
    /// Gets the water block state.
    /// Equivalent to Java's Blocks.WATER.defaultBlockState().
    /// </summary>
    public static BlockState? Water => GetBlockState("minecraft:water");

    /// <summary>
    /// Gets the lava block state.
    /// Equivalent to Java's Blocks.LAVA.defaultBlockState().
    /// </summary>
    public static BlockState? Lava => GetBlockState("minecraft:lava");

    /// <summary>
    /// Gets a block state by name from the registry.
    /// Returns null if the block is not found.
    /// </summary>
    private static BlockState? GetBlockState(string name)
    {
        if (ClientState.BlockStateRegistry == null)
            return null;

        foreach (var kvp in ClientState.BlockStateRegistry)
        {
            if (kvp.Value.Name == name)
            {
                return kvp.Value;
            }
        }

        return null;
    }
}

