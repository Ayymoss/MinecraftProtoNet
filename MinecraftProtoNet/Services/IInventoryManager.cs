using MinecraftProtoNet.Models.World.Chunk;

namespace MinecraftProtoNet.Services;

public interface IInventoryManager
{
    /// <summary>
    /// Finds and equips the best tool for the given block.
    /// Returns true if a valid tool was found and equipped (or if hand is best).
    /// </summary>
    Task<bool> EquipBestTool(BlockState block);
    Task<bool> EquipItemMatches(IEnumerable<string> itemNames);

    /// <summary>
    /// Moves an item from a source slot to a destination slot.
    /// </summary>
    Task SwapItems(int fromSlot, int toSlot);
    
    /// <summary>
    /// Selects the specified hotbar slot (0-8).
    /// </summary>
    Task SetHotbarSlot(int hotbarSlot);

    /// <summary>
    /// Gets the current efficiency multiplier for the held item against the specified block.
    /// </summary>
    float GetDigSpeed(BlockState block);

    /// <summary>
    /// Calculates the best possible dig speed using available tools in the inventory.
    /// </summary>
    float GetBestDigSpeed(BlockState block);
}
