namespace MinecraftProtoNet.Core.Models.World.Chunk;

/// <summary>
/// Registry of block physics data matching Mojang's Block.Properties.
/// Data sourced from minecraft-26.1-REFERENCE-ONLY.
/// </summary>
public static class BlockPhysicsData
{
    /// <summary>
    /// Block physics: (Friction, SpeedFactor, JumpFactor, HasCollision, DestroySpeed)
    /// </summary>
    private static readonly Dictionary<string, BlockData> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        // ===== Ice Blocks (high friction = slippery) =====
        ["minecraft:ice"] = new(0.98f, 1.0f, 1.0f, true, 0.5f),
        ["minecraft:packed_ice"] = new(0.98f, 1.0f, 1.0f, true, 0.5f),
        ["minecraft:blue_ice"] = new(0.989f, 1.0f, 1.0f, true, 2.8f),
        ["minecraft:frosted_ice"] = new(0.98f, 1.0f, 1.0f, true, 0.5f),

        // ===== Slime & Honey (bouncy/sticky) =====
        ["minecraft:slime_block"] = new(0.8f, 0.4f, 1.0f, true, 0.0f),
        ["minecraft:honey_block"] = new(0.8f, 0.4f, 0.5f, true, 0.0f),

        // ===== Soul Sand/Soil (slow movement) =====
        ["minecraft:soul_sand"] = new(0.6f, 0.4f, 1.0f, true, 0.5f),
        ["minecraft:soul_soil"] = new(0.6f, 0.4f, 1.0f, true, 0.5f),

        // ===== Air blocks (no collision) =====
        ["minecraft:air"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:cave_air"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:void_air"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),

        // ===== Passable plants (no collision) =====
        ["minecraft:grass"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:short_grass"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:tall_grass"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:fern"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:large_fern"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:dead_bush"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:seagrass"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:tall_seagrass"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:kelp"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:kelp_plant"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:vine"] = new(0.6f, 1.0f, 1.0f, false, 0.2f),
        ["minecraft:glow_lichen"] = new(0.6f, 1.0f, 1.0f, false, 0.2f),
        ["minecraft:hanging_roots"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),

        // ===== Flowers (no collision) =====
        ["minecraft:dandelion"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:poppy"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:blue_orchid"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:allium"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:azure_bluet"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:red_tulip"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:orange_tulip"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:white_tulip"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:pink_tulip"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:oxeye_daisy"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:cornflower"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:lily_of_the_valley"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:wither_rose"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:sunflower"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:lilac"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:rose_bush"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:peony"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:pink_petals"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:torchflower"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:pitcher_plant"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),

        // ===== Saplings (no collision) =====
        ["minecraft:oak_sapling"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:spruce_sapling"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:birch_sapling"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:jungle_sapling"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:acacia_sapling"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:dark_oak_sapling"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:cherry_sapling"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:mangrove_propagule"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:bamboo_sapling"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),

        // ===== Mushrooms (no collision) =====
        ["minecraft:brown_mushroom"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:red_mushroom"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:crimson_fungus"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:warped_fungus"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:crimson_roots"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:warped_roots"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:nether_sprouts"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),

        // ===== Torches & Decorations (no collision) =====
        ["minecraft:torch"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:wall_torch"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:soul_torch"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:soul_wall_torch"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:redstone_torch"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:redstone_wall_torch"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:end_rod"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),

        // ===== Rails (no collision) =====
        ["minecraft:rail"] = new(0.6f, 1.0f, 1.0f, false, 0.7f),
        ["minecraft:powered_rail"] = new(0.6f, 1.0f, 1.0f, false, 0.7f),
        ["minecraft:detector_rail"] = new(0.6f, 1.0f, 1.0f, false, 0.7f),
        ["minecraft:activator_rail"] = new(0.6f, 1.0f, 1.0f, false, 0.7f),

        // ===== Redstone Components (no collision) =====
        ["minecraft:redstone_wire"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:lever"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:tripwire"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:tripwire_hook"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),

        // ===== Sugar Cane & Similar (no collision) =====
        ["minecraft:sugar_cane"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:fire"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:soul_fire"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),
        ["minecraft:nether_portal"] = new(0.6f, 1.0f, 1.0f, false, -1.0f),

        // ===== Water (liquid, no collision) =====
        ["minecraft:water"] = new(0.6f, 1.0f, 1.0f, false, 100.0f),
        ["minecraft:lava"] = new(0.6f, 1.0f, 1.0f, false, 100.0f),

        // ===== Cobweb (special - has collision but doesn't block motion) =====
        ["minecraft:cobweb"] = new(0.6f, 0.25f, 1.0f, true, 4.0f),

        // ===== Ladders & Climbables =====
        ["minecraft:ladder"] = new(0.6f, 1.0f, 1.0f, false, 0.4f),
        ["minecraft:scaffolding"] = new(0.6f, 1.0f, 1.0f, false, 0.0f),

        // ===== Bedrock (unbreakable) =====
        ["minecraft:bedrock"] = new(0.6f, 1.0f, 1.0f, true, -1.0f),

        // ===== Pressure Plates (no collision) =====
        ["minecraft:stone_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:oak_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:spruce_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:birch_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:jungle_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:acacia_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:dark_oak_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:cherry_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:mangrove_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:bamboo_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:crimson_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:warped_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:polished_blackstone_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:light_weighted_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:heavy_weighted_pressure_plate"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),

        // ===== Buttons (no collision) =====
        ["minecraft:stone_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:oak_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:spruce_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:birch_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:jungle_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:acacia_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:dark_oak_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:cherry_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:mangrove_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:bamboo_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:crimson_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:warped_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),
        ["minecraft:polished_blackstone_button"] = new(0.6f, 1.0f, 1.0f, false, 0.5f),

        // ===== Signs (no collision) =====
        ["minecraft:oak_sign"] = new(0.6f, 1.0f, 1.0f, false, 1.0f),
        ["minecraft:oak_wall_sign"] = new(0.6f, 1.0f, 1.0f, false, 1.0f),
        ["minecraft:oak_hanging_sign"] = new(0.6f, 1.0f, 1.0f, false, 1.0f),
        ["minecraft:oak_wall_hanging_sign"] = new(0.6f, 1.0f, 1.0f, false, 1.0f),
    };

    /// <summary>
    /// Applies physics data from registry to a BlockState.
    /// </summary>
    public static void ApplyTo(BlockState state)
    {
        if (Registry.TryGetValue(state.Name, out var data))
        {
            state.Friction = data.Friction;
            state.SpeedFactor = data.SpeedFactor;
            state.JumpFactor = data.JumpFactor;
            state.HasCollision = data.HasCollision;
            state.DestroySpeed = data.DestroySpeed;
        }
        else
        {
            // Apply fallback based on name patterns
            ApplyFallbacks(state);
        }
    }

    /// <summary>
    /// Fallback logic for blocks not in registry.
    /// </summary>
    private static void ApplyFallbacks(BlockState state)
    {
        var name = state.Name;

        // Saplings
        if (name.EndsWith("_sapling", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
        // Flowers
        else if (name.EndsWith("_flower", StringComparison.OrdinalIgnoreCase) || 
                 name.Contains("flower", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
        // Signs
        else if (name.Contains("_sign", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
        // Buttons
        else if (name.EndsWith("_button", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
        // Pressure plates
        else if (name.EndsWith("_pressure_plate", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
        // Torches
        else if (name.Contains("torch", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
        // Rails
        else if (name.Contains("rail", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
        // Carpet
        else if (name.EndsWith("_carpet", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
        // Coral fans
        else if (name.Contains("_coral_fan", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
        // Crops
        else if (name.Equals("minecraft:wheat", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("minecraft:carrots", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("minecraft:potatoes", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("minecraft:beetroots", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("minecraft:melon_stem", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("minecraft:pumpkin_stem", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("minecraft:sweet_berry_bush", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
    }

    /// <summary>
    /// Block physics data record.
    /// </summary>
    private readonly record struct BlockData(
        float Friction,
        float SpeedFactor,
        float JumpFactor,
        bool HasCollision,
        float DestroySpeed);
}
