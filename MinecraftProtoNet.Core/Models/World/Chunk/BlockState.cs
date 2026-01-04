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
    /// Time to break this block (destroy speed).
    /// -1 = unbreakable (bedrock), 0 = instant break
    /// </summary>
    public float DestroySpeed { get; set; } = 1.0f;

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

    public bool IsSlab => Name.Contains("slab", StringComparison.OrdinalIgnoreCase);
    public bool IsStairs => Name.Contains("stairs", StringComparison.OrdinalIgnoreCase);
    public bool IsSnow => Name.Equals("minecraft:snow", StringComparison.OrdinalIgnoreCase);

    public int SnowLayers => Properties.TryGetValue("layers", out var layers) && int.TryParse(layers, out var count) ? count : 0;

    /// <summary>
    /// Whether this block is a liquid (water/lava).
    /// </summary>
    public bool IsLiquid => Name.Contains("water", StringComparison.OrdinalIgnoreCase) ||
                            Name.Contains("lava", StringComparison.OrdinalIgnoreCase);

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
    /// Reference: minecraft-26.1-REFERENCE-ONLY - BlockBehaviour.Properties.requiresCorrectToolForDrops()
    /// Most blocks don't require a specific tool, but some (like ores) do.
    /// </summary>
    public bool RequiresCorrectToolForDrops { get; set; } = false;

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
