using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Models.World.Chunk;

/// <summary>
/// Registry of block physics data matching Mojang's Block.Properties.
/// Non-default friction/speedFactor/jumpFactor values are kept here because
/// datagen does not export these (they're hardcoded in Java's BlockBehaviour.Properties).
/// HasCollision is now derived from datagen block tags where possible.
/// </summary>
public static class BlockPhysicsData
{
    /// <summary>
    /// Block physics: (Friction, SpeedFactor, JumpFactor, HasCollision, DestroySpeed)
    /// Only blocks with NON-DEFAULT friction, speedFactor, or jumpFactor are listed here.
    /// HasCollision for most blocks is derived from tags (see ApplyTagBasedCollision).
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

        // ===== Cobweb (special - has collision but doesn't block motion, slow speed) =====
        ["minecraft:cobweb"] = new(0.6f, 0.25f, 1.0f, true, 4.0f),

        // ===== Bedrock (unbreakable) =====
        ["minecraft:bedrock"] = new(0.6f, 1.0f, 1.0f, true, -1.0f),

        // ===== Liquids =====
        ["minecraft:water"] = new(0.6f, 1.0f, 1.0f, false, 100.0f),
        ["minecraft:lava"] = new(0.6f, 1.0f, 1.0f, false, 100.0f),
    };

    /// <summary>
    /// Block tags that indicate no collision (blocks the player can walk through).
    /// These are loaded from datagen tag files in StaticFiles/data/minecraft/tags/block/.
    /// </summary>
    private static readonly string[] NoCollisionTags =
    [
        "replaceable",      // air, water, lava, grass, fern, dead_bush, vines, etc.
        "buttons",          // all button variants
        "all_signs",        // all sign and hanging sign variants
        "pressure_plates",  // all pressure plate variants
        "rails",            // rail, powered_rail, detector_rail, activator_rail
    ];

    /// <summary>
    /// Individual blocks with no collision that aren't covered by tags above.
    /// </summary>
    private static readonly HashSet<string> AdditionalNoCollisionBlocks = new(StringComparer.OrdinalIgnoreCase)
    {
        // Torches
        "minecraft:torch", "minecraft:wall_torch",
        "minecraft:soul_torch", "minecraft:soul_wall_torch",
        "minecraft:redstone_torch", "minecraft:redstone_wall_torch",
        // Redstone
        "minecraft:redstone_wire", "minecraft:lever",
        "minecraft:tripwire", "minecraft:tripwire_hook",
        // Misc passable
        "minecraft:end_rod", "minecraft:sugar_cane",
        "minecraft:fire", "minecraft:soul_fire",
        "minecraft:nether_portal", "minecraft:ladder", "minecraft:scaffolding",
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
            // Derive HasCollision from tags and known no-collision blocks
            ApplyTagBasedCollision(state);
        }
    }

    /// <summary>
    /// Uses datagen block tags to determine if a block has collision.
    /// Falls back to name-based heuristics for blocks not covered by tags.
    /// </summary>
    private static void ApplyTagBasedCollision(BlockState state)
    {
        var name = state.Name;

        // Check tag-based no-collision
        foreach (var tag in NoCollisionTags)
        {
            if (ClientState.BlockTags.HasTag(name, tag))
            {
                state.HasCollision = false;
                return;
            }
        }

        // Check individual no-collision blocks
        if (AdditionalNoCollisionBlocks.Contains(name))
        {
            state.HasCollision = false;
            return;
        }

        // Heuristic fallbacks for blocks that may not be in tags
        // (flowers, saplings, mushrooms, crops, coral fans)
        if (name.EndsWith("_sapling", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("_flower", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("_coral_fan", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:wheat", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:carrots", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:potatoes", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:beetroots", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:melon_stem", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:pumpkin_stem", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:sweet_berry_bush", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:brown_mushroom", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:red_mushroom", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:crimson_fungus", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:warped_fungus", StringComparison.OrdinalIgnoreCase))
        {
            state.HasCollision = false;
        }
    }

    private readonly record struct BlockData(
        float Friction,
        float SpeedFactor,
        float JumpFactor,
        bool HasCollision,
        float DestroySpeed);
}
