using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Models.World.Chunk;

/// <summary>
/// Represents a block state with properties aligned to Mojang's BlockBehaviour.BlockStateBase.
/// </summary>
public class BlockState
{
    // ===== Identity =====
    public int Id { get; }
    public string Name { get; }
    public Dictionary<string, string> Properties { get; }

    // ===== Physics Properties (from Mojang Block.Properties) =====
    
    /// <summary>
    /// Whether this block has collision (most blocks do).
    /// Set via registry or defaults to true for non-air/liquid blocks.
    /// </summary>
    public bool HasCollision { get; set; } = true;

    /// <summary>
    /// Block friction (0.0-1.0). Default is 0.6.
    /// Ice = 0.98, Blue Ice = 0.989, Slime = 0.8
    /// </summary>
    public float Friction { get; set; } = 0.6f;

    /// <summary>
    /// Movement speed multiplier on this block. Default is 1.0.
    /// Soul Sand = 0.4, Honey = 0.4
    /// </summary>
    public float SpeedFactor { get; set; } = 1.0f;

    /// <summary>
    /// Jump height multiplier on this block. Default is 1.0.
    /// Honey = 0.5
    /// </summary>
    public float JumpFactor { get; set; } = 1.0f;

    /// <summary>
    /// Time to break this block (destroy speed / hardness). -1 = unbreakable (bedrock), 0 = instant break.
    /// Sourced from the generated <see cref="ClientState.BlockHardness"/> table (MC 26.2 Blocks.java);
    /// falls back to 1.0 if the table isn't loaded or the block is unknown.
    /// </summary>
    public float DestroySpeed =>
        ClientState.BlockHardness.TryGetValue(Name, out var h) ? h.Hardness : 1.0f;

    /// <summary>
    /// Light level emitted by this block (0-15).
    /// </summary>
    public int LightEmission { get; set; }

    // ===== Computed Flags =====

    /// <summary>
    /// Whether this block is air.
    /// </summary>
    public bool IsAir => Id == 0 || Name.EndsWith("air", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this is a top slab or top half block.
    /// </summary>
    public bool IsTop
    {
        get
        {
            if (Properties.TryGetValue("type", out var type)) return type == "top" || type == "double";
            if (Properties.TryGetValue("half", out var half)) return half == "top";
            return false;
        }
    }

    public bool IsSlab => ClientState.BlockTags.HasTag(Name, "slabs");
    public bool IsStairs => ClientState.BlockTags.HasTag(Name, "stairs");
    public bool IsSnow => Name.Equals("minecraft:snow", StringComparison.OrdinalIgnoreCase);

    public int SnowLayers => Properties.TryGetValue("layers", out var layers) && int.TryParse(layers, out var count) ? count : 0;

    // ===== Block-class identity (from the vanilla data report "definition", not heuristics) =====

    // Block-type ids matching the relevant Java Block subclasses.
    private static readonly HashSet<string> ClimbableBlocks =
    [
        "minecraft:ladder", "minecraft:vine",
        "minecraft:weeping_vines", "minecraft:weeping_vines_plant",
        "minecraft:twisting_vines", "minecraft:twisting_vines_plant",
    ];

    // Block-type ids whose Java block class extends FallingBlock.
    private static readonly HashSet<string> FallingBlockTypes =
    [
        "minecraft:sand", "minecraft:colored_falling", "minecraft:concrete_powder",
        "minecraft:anvil", "minecraft:dragon_egg",
    ];

    /// <summary>
    /// Block-class identity (e.g. minecraft:slab, minecraft:ladder, minecraft:liquid) from the data
    /// report's "definition.type"; the non-heuristic equivalent of the Java Block subclass. Null if unknown.
    /// </summary>
    public string? BlockType =>
        ClientState.BlockDefinitions.TryGetValue(Name, out var def) ? def.Type : null;

    /// <summary>
    /// The fluid id for liquid blocks (minecraft:water / minecraft:lava); null for non-liquids.
    /// </summary>
    public string? Fluid =>
        ClientState.BlockDefinitions.TryGetValue(Name, out var def) ? def.Fluid : null;

    /// <summary>
    /// Whether this block is a pure liquid block (a water or lava block). Mirrors Mojang's notion of a
    /// liquid block (NOT "contains a fluid" — waterlogged solid blocks are not liquids here).
    /// </summary>
    public bool IsLiquid => BlockType == "minecraft:liquid";

    /// <summary>
    /// Whether this block's fluid state is water — i.e. a water block OR a waterlogged block.
    /// Reference: MovementHelper.isWater (getFluidState().getType() == WATER/FLOWING_WATER).
    /// </summary>
    public bool IsWater =>
        (BlockType == "minecraft:liquid" && Fluid == "minecraft:water") ||
        (Properties.TryGetValue("waterlogged", out var w) && w == "true");

    /// <summary>
    /// Whether this block's fluid state is lava. Reference: MovementHelper.isLava.
    /// </summary>
    public bool IsLava => BlockType == "minecraft:liquid" && Fluid == "minecraft:lava";

    /// <summary>Whether this block is climbable (ladder/vine family). Reference: MovementHelper.isClimbable.</summary>
    public bool IsClimbable => ClimbableBlocks.Contains(Name);

    /// <summary>Whether this block's class extends FallingBlock (sand, gravel, concrete powder, anvil, dragon egg).</summary>
    public bool IsFallingBlock => BlockType != null && FallingBlockTypes.Contains(BlockType);

    /// <summary>Whether this block is a carpet (CarpetBlock family: wool and moss carpets).</summary>
    public bool IsCarpet => BlockType is "minecraft:carpet" or "minecraft:wool_carpet";

    /// <summary>Whether this block is a lily pad.</summary>
    public bool IsLilyPad => BlockType == "minecraft:lily_pad";

    /// <summary>Whether this block is a fence gate.</summary>
    public bool IsFenceGate => BlockType == "minecraft:fence_gate";

    /// <summary>Whether this block is a door (any material, incl. iron). Reference: DoorBlock.</summary>
    public bool IsDoor => BlockType == "minecraft:door";

    /// <summary>Whether this block is a ladder.</summary>
    public bool IsLadder => BlockType == "minecraft:ladder";

    /// <summary>Whether this block is soul sand.</summary>
    public bool IsSoulSand => BlockType == "minecraft:soul_sand";

    /// <summary>Whether this block is a magma block.</summary>
    public bool IsMagmaBlock => BlockType == "minecraft:magma";

    /// <summary>
    /// Whether this block physically blocks movement (has a collision box).
    /// Aligned with Mojang's blocksMotion() method.
    /// </summary>
    public bool BlocksMotion
    {
        get
        {
            if (IsAir || IsLiquid) return false;
            if (!HasCollision) return false;
            
            // Special cases from Mojang's blocksMotion() - cobweb and bamboo don't block
            if (Name.Contains("cobweb", StringComparison.OrdinalIgnoreCase)) return false;
            if (Name.Contains("bamboo_sapling", StringComparison.OrdinalIgnoreCase)) return false;
            
            return true;
        }
    }

    /// <summary>
    /// Legacy property, now uses BlocksMotion.
    /// </summary>
    public bool IsSolid => BlocksMotion;

    /// <summary>
    /// Whether this block is nearly impossible to break (e.g. Bedrock).
    /// </summary>
    public bool IsExhaustinglyDifficultToBreak => DestroySpeed < 0;

    /// <summary>
    /// Whether this block requires the correct tool to drop items when broken.
    /// Reference: BlockBehaviour.Properties.requiresCorrectToolForDrops(). Sourced from the generated
    /// <see cref="ClientState.BlockHardness"/> table.
    /// </summary>
    public bool RequiresCorrectToolForDrops =>
        ClientState.BlockHardness.TryGetValue(Name, out var h) && h.RequiresCorrectTool;

    // ===== Constructor =====

    public BlockState(int id, string name, Dictionary<string, string>? properties = null)
    {
        Id = id;
        Name = name;
        Properties = properties ?? [];

        // Apply physics data from registry
        BlockPhysicsData.ApplyTo(this);
    }

    // ===== Object Overrides =====

    public override bool Equals(object? obj)
    {
        if (obj is BlockState other)
        {
            return Id == other.Id;
        }
        return false;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => $"BlockState({Id}, {Name})";
}
