namespace MinecraftProtoNet.Models.World.Chunk;

public class BlockState(int id, string name)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public bool IsAir => Id is 0 || Name.EndsWith("air", StringComparison.OrdinalIgnoreCase);

    public bool IsLiquid => Name.Contains("water", StringComparison.CurrentCultureIgnoreCase) ||
                            Name.Contains("lava", StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Whether this block physically blocks movement (has a collision box in Minecraft).
    /// </summary>
    public bool BlocksMotion => !IsAir && !IsLiquid && !IsPassable(Name);

    /// <summary>
    /// Legacy property, now uses BlocksMotion.
    /// </summary>
    public bool IsSolid => BlocksMotion;

    private static bool IsPassable(string name)
    {
        var lowerName = name.ToLower();
        
        // Exclude inherently solid block types that might contain passable keywords
        if (lowerName.Contains("block") || 
            lowerName.Contains("slab") || 
            lowerName.Contains("stairs") || 
            lowerName.Contains("wall") || 
            lowerName.Contains("fence") ||
            lowerName.Contains("gate") ||
            lowerName.Contains("door") ||
            lowerName.Contains("button") ||
            lowerName.Contains("pressure_plate"))
            return false;

        return lowerName.Contains("sapling") || 
               lowerName.Contains("flower") || 
               lowerName == "grass" || 
               lowerName == "tall_grass" ||
               lowerName.EndsWith("_grass") ||
               lowerName.Contains("fern") || 
               lowerName == "mushroom" ||
               lowerName.EndsWith("_mushroom") ||
               lowerName.Contains("poppy") || 
               lowerName.Contains("dandelion") || 
               lowerName.EndsWith("_plant") ||
               lowerName.Contains("sugar_cane") ||
               lowerName.Contains("torch") ||
               lowerName.Contains("lever") ||
               lowerName.Contains("redstone") ||
               lowerName.Contains("vine") ||
               lowerName.Contains("dead_bush") ||
               lowerName.Contains("deadbush") ||
               lowerName.Contains("fire") ||
               lowerName.Contains("rail") ||
               lowerName.Contains("glow_lichen") ||
               lowerName.Contains("hanging_roots") ||
               lowerName.Contains("hanging_sign") ||
               lowerName.Contains("azalea_bush") ||
               lowerName.Contains("pink_petals") ||
               lowerName.Contains("spore_blossom") ||
               lowerName.Contains("sculk_vein") ||
               lowerName.Contains("sunflower") ||
               lowerName.Contains("lilac") ||
               lowerName.Contains("rose_bush") ||
               lowerName.Contains("peony") ||
               lowerName.Contains("item_frame") ||
               lowerName.Contains("glow_item_frame") ||
               lowerName.Contains("end_rod");
    }

    public override bool Equals(object? obj)
    {
        if (obj is BlockState other)
        {
            return Id == other.Id;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return $"BlockState({Id})";
    }
}
