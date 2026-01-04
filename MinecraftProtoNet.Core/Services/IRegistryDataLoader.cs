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
    /// Loads item registry from static files.
    /// </summary>
    /// <returns>A dictionary mapping protocol IDs to item names.</returns>
    Task<Dictionary<int, string>> LoadItemsAsync();
}
