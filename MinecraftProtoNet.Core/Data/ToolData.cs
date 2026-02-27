using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Data;

/// <summary>
/// Provides data about tools, their tiers, and effectiveness against blocks.
/// Tool-block effectiveness is derived from datagen mineable tags where available.
/// </summary>
public static class ToolData
{
    public enum ToolType
    {
        None,
        Hand,
        Pickaxe,
        Axe,
        Shovel,
        Hoe,
        Sword,
        Shears
    }

    public enum ToolTier
    {
        None = 0,
        Wood = 1,
        Stone = 2,
        Iron = 3,
        Diamond = 4,
        Netherite = 5,
        Gold = 6
    }

    /// <summary>
    /// Gets the tool type of an item by its protocol ID (or name if mapped).
    /// </summary>
    public static ToolType GetToolType(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return ToolType.None;
        if (itemName.Contains("_pickaxe")) return ToolType.Pickaxe;
        if (itemName.Contains("_axe")) return ToolType.Axe; // Note: pickaxe contains axe, check pickaxe first
        if (itemName.Contains("_shovel")) return ToolType.Shovel;
        if (itemName.Contains("_hoe")) return ToolType.Hoe;
        if (itemName.Contains("_sword")) return ToolType.Sword;
        if (itemName == "minecraft:shears") return ToolType.Shears;
        return ToolType.None;
    }

    /// <summary>
    /// Gets the tier of a tool.
    /// </summary>
    public static ToolTier GetToolTier(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return ToolTier.None;
        
        if (itemName.StartsWith("minecraft:wooden_")) return ToolTier.Wood;
        if (itemName.StartsWith("minecraft:stone_")) return ToolTier.Stone;
        if (itemName.StartsWith("minecraft:iron_")) return ToolTier.Iron;
        if (itemName.StartsWith("minecraft:diamond_")) return ToolTier.Diamond;
        if (itemName.StartsWith("minecraft:netherite_")) return ToolTier.Netherite;
        if (itemName.StartsWith("minecraft:golden_")) return ToolTier.Gold;
        
        return ToolTier.None;
    }

    /// <summary>
    /// Gets the mining speed multiplier for a tool tier.
    /// </summary>
    public static float GetSpeed(ToolTier tier)
    {
        return tier switch
        {
            ToolTier.Wood => 2.0f,
            ToolTier.Stone => 4.0f,
            ToolTier.Iron => 6.0f,
            ToolTier.Diamond => 8.0f,
            ToolTier.Netherite => 9.0f,
            ToolTier.Gold => 12.0f,
            _ => 1.0f // Hand
        };
    }

    /// <summary>
    /// Maps ToolType to the corresponding datagen mineable tag name.
    /// </summary>
    private static readonly Dictionary<ToolType, string> ToolToMineableTag = new()
    {
        [ToolType.Pickaxe] = "mineable/pickaxe",
        [ToolType.Axe] = "mineable/axe",
        [ToolType.Shovel] = "mineable/shovel",
        [ToolType.Hoe] = "mineable/hoe",
    };

    /// <summary>
    /// Determines if a tool is "effective" (correct tool) for a given block.
    /// Uses datagen mineable tags from StaticFiles/data/minecraft/tags/block/mineable/.
    /// Shears effectiveness uses a name-based check since datagen doesn't export a shears tag.
    /// </summary>
    public static bool IsCorrectTool(ToolType tool, BlockState block)
    {
        if (block.IsExhaustinglyDifficultToBreak) return false;

        // Shears don't have a mineable tag — use simple name check
        if (tool == ToolType.Shears)
        {
            var name = block.Name;
            return name.Contains("leaves") || name.Contains("wool") ||
                   name.Contains("cobweb") || name.Contains("vine");
        }

        if (ToolToMineableTag.TryGetValue(tool, out var tagName))
        {
            return ClientState.BlockTags.HasTag(block.Name, tagName);
        }

        return false;
    }
}
