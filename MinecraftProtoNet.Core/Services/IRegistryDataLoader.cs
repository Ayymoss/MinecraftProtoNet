using MinecraftProtoNet.Core.Models.World.Chunk;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Provides access to static game data files for blocks, biomes, and items.
/// </summary>
public interface IRegistryDataLoader
{
    /// <summary>
    /// Loads the block state registry from static files.
    /// </summary>
    /// <returns>A dictionary mapping state IDs to BlockState objects.</returns>
    Task<Dictionary<int, BlockState>> LoadBlockStatesAsync();

    /// <summary>
    /// Loads block-class definitions (block-type identity + fluid) from static files.
    /// </summary>
    /// <returns>A dictionary mapping block names to their <see cref="BlockDefinition"/>.</returns>
    Task<Dictionary<string, BlockDefinition>> LoadBlockDefinitionsAsync();

    /// <summary>
    /// Loads per-block hardness (destroy speed) + requiresCorrectToolForDrops from static files.
    /// </summary>
    /// <returns>A dictionary mapping block names to their <see cref="BlockHardness"/>.</returns>
    Task<Dictionary<string, BlockHardness>> LoadBlockHardnessAsync();

    /// <summary>
    /// Loads item registry from static files.
    /// </summary>
    /// <returns>A dictionary mapping protocol IDs to item names.</returns>
    Task<Dictionary<int, string>> LoadItemsAsync();

    /// <summary>
    /// Loads entity type registry from static files.
    /// </summary>
    /// <returns>A dictionary mapping protocol IDs to entity type names.</returns>
    Task<Dictionary<int, string>> LoadEntityTypesAsync();

    /// <summary>Attribute registry (protocol id -> name) from the static report; never sent by servers.</summary>
    Task<Dictionary<int, string>> LoadAttributesAsync();
}
