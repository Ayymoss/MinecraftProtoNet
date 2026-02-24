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
    /// <summary>
    /// Strips variant suffixes (_stairs, _slab, _wall, _fence, _fence_gate, _button, _pressure_plate)
    /// to get the base material name for tool matching.
    /// e.g. "minecraft:brick_stairs" → "minecraft:brick", "minecraft:oak_stairs" → "minecraft:oak"
    /// </summary>
    private static string GetBaseMaterial(string name)
    {
        // Order matters: check longer suffixes first
        string[] suffixes = ["_stairs", "_slab", "_wall", "_fence_gate", "_fence", "_button", "_pressure_plate"];
        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return name[..^suffix.Length];
            }
        }
        return name;
    }

    public static bool IsCorrectTool(ToolType tool, BlockState block)
    {
        // Material checks based on block names/types
        var name = block.Name;
        // Also check the base material for variant blocks (stairs, slabs, walls, etc.)
        var baseName = GetBaseMaterial(name);

        if (block.IsExhaustinglyDifficultToBreak) return false; // Bedrock etc.

        // Pickaxe
        if (tool == ToolType.Pickaxe)
        {
            if (name.Contains("stone") || name.Contains("cobblestone") || name.Contains("andesite") ||
                name.Contains("granite") || name.Contains("diorite") || name.Contains("ore") ||
                (name.Contains("block") && (name.Contains("iron") || name.Contains("gold") || name.Contains("diamond"))) ||
                name.Contains("brick") || name.Contains("concrete") || name.Contains("terracotta") ||
                name.Contains("furnace") || name.Contains("anvil") || name.Contains("rail") ||
                name.Contains("obsidian") || name.Contains("spawner") || name.Contains("prismarine") ||
                name.Contains("lantern") || name.Contains("cauldron") || name.Contains("hopper") ||
                name.Contains("sandstone") || name.Contains("deepslate") || name.Contains("copper") ||
                name.Contains("blackstone") || name.Contains("basalt") || name.Contains("tuff") ||
                name.Contains("calcite") || name.Contains("dripstone") || name.Contains("amethyst") ||
                name.Contains("purpur") || name.Contains("quartz") || name.Contains("end_stone"))
            {
                return true;
            }
            // Check base material for variants (e.g. "minecraft:brick" from "minecraft:brick_stairs")
            if (baseName != name && IsCorrectTool(tool, new BlockState(0, baseName)))
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
                name.Contains("crafting_table") || name.Contains("bamboo") || name.Contains("stem") ||
                name.Contains("hyphae") || name.Contains("mushroom_block"))
            {
                return true;
            }
            // Check base material for variants (e.g. "minecraft:oak" from "minecraft:oak_stairs")
            if (baseName != name && IsCorrectTool(tool, new BlockState(0, baseName)))
            {
                return true;
            }
        }

        // Shovel
        if (tool == ToolType.Shovel)
        {
            // "sand" must not match "sandstone" — sandstone is a pickaxe block
            if (name.Contains("dirt") || name.Contains("grass") ||
                (name.Contains("sand") && !name.Contains("sandstone")) ||
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
