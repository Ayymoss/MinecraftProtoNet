using MinecraftProtoNet.Core.Models.World.Chunk;

namespace MinecraftProtoNet.Core.Data;

/// <summary>
/// Provides data about tools, their tiers, and effectiveness against blocks.
/// Data sourced from Minecraft 1.21 mechanics.
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
    /// Determines if a tool is "effective" (correct tool) for a given block.
    /// This is a simplified lookup. Ideally, this would be data-driven or tag-based.
    /// </summary>
    public static bool IsCorrectTool(ToolType tool, BlockState block)
    {
        // Material checks based on block names/types
        var name = block.Name;

        if (block.IsExhaustinglyDifficultToBreak) return false; // Bedrock etc.

        // Pickaxe
        if (tool == ToolType.Pickaxe)
        {
            if (name.Contains("stone") || name.Contains("cobblestone") || name.Contains("andesite") ||
                name.Contains("granite") || name.Contains("diorite") || name.Contains("ore") ||
                name.Contains("block") && (name.Contains("iron") || name.Contains("gold") || name.Contains("diamond")) ||
                name.Contains("brick") || name.Contains("concrete") || name.Contains("terracotta") ||
                name.Contains("furnace") || name.Contains("anvil") || name.Contains("rail") ||
                name.Contains("obsidian") || name.Contains("spawner") || name.Contains("prismarine") ||
                name.Contains("lantern") || name.Contains("cauldron") || name.Contains("hopper"))
            {
                return true;
            }
        }

        // Axe
        if (tool == ToolType.Axe)
        {
            if (name.Contains("log") || name.Contains("planks") || name.Contains("wood") ||
                name.Contains("chest") || name.Contains("barrel") || name.Contains("fence") ||
                name.Contains("gate") || name.Contains("sign") || name.Contains("banner") ||
                name.Contains("bookshelf") || name.Contains("loom") || name.Contains("composter") ||
                name.Contains("crafting_table"))
            {
                return true;
            }
        }

        // Shovel
        if (tool == ToolType.Shovel)
        {
            if (name.Contains("dirt") || name.Contains("grass") || name.Contains("sand") ||
                name.Contains("gravel") || name.Contains("clay") || name.Contains("snow") ||
                name.Contains("mud") || name.Contains("soul_sand") || name.Contains("soul_soil") ||
                name.Contains("concrete_powder"))
            {
                return true;
            }
        }

        // Hoe
        if (tool == ToolType.Hoe)
        {
            if (name.Contains("leaves") || name.Contains("hay_block") || name.Contains("sculk") ||
                name.Contains("sponge") || name.Contains("wart_block") || name.Contains("shroomlight"))
            {
                return true;
            }
        }

        // Shears
        if (tool == ToolType.Shears)
        {
            if (name.Contains("leaves") || name.Contains("wool") || name.Contains("cobweb") ||
                name.Contains("vine"))
            {
                return true;
            }
        }

        // Some blocks require no specific tool or any tool works (dirt/wood technically don't REQUIRE tool to drop, but have speed boost)
        // This method strictly returns if the tool applies its speed bonus / is the intended tool class.
        return false;
    }
}
