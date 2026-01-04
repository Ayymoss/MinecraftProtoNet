using MinecraftProtoNet.Core.Models.World.Chunk;

namespace MinecraftProtoNet.Core.Handlers.Meta;

/// <summary>
/// Helper for determining PathType from BlockState.
/// Based on Java's WalkNodeEvaluator logic.
/// </summary>
public static class PathfindingContext
{
    public static PathType GetPathTypeFromState(BlockState? blockState)
    {
        if (blockState == null || blockState.IsAir)
        {
            return PathType.Open;
        }

        var name = blockState.Name.ToLower();

        // 1. Liquids
        if (name.Contains("lava")) return PathType.Lava;
        if (name.Contains("water")) return PathType.Water;

        // 2. Dangerous blocks
        if (name.Contains("fire") || name.Contains("magma")) return PathType.DamageFire;
        if (name.Contains("cactus") || name.Contains("berry_bush")) return PathType.DamageOther;
        if (name.Contains("wither_rose") || name.Contains("pointed_dripstone")) return PathType.DamageCautious;

        // 3. Special Pathing Blocks
        if (name.Contains("fence") || name.Contains("wall") || name.Contains("fence_gate"))
        {
            // Note: In Java, open fence gates are walkable, closed are fence.
            // Since we don't have metadata yet, we assume all gates are closed for safety.
            return PathType.Fence; 
        }

        if (name.Contains("rail")) return PathType.Rail;
        if (name.Contains("leaves")) return PathType.Leaves;
        if (name.Contains("honey_block")) return PathType.StickyHoney;
        if (name.Contains("powder_snow")) return PathType.PowderSnow;
        if (name.Contains("trapdoor")) return PathType.Trapdoor;

        // 4. Doors
        if (name.Contains("door"))
        {
            if (name.Contains("iron")) return PathType.DoorIronClosed;
            // For wood doors, we check if they are open. 
            // Again, lacking property metadata (lit, open, half), we are cautious.
            return PathType.DoorWoodClosed;
        }

        // 5. Solid Blocks
        // We use the centralized BlocksMotion property to determine if this block
        // physically obstructs movement (e.g., stone blocks vs dead bushes).
        if (blockState.BlocksMotion)
        {
            return PathType.Blocked;
        }

        // 6. Non-blocking blocks (Air, Passable Vegetation, etc.)
        return PathType.Open;
    }
}
