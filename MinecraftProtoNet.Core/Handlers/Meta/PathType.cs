namespace MinecraftProtoNet.Core.Handlers.Meta;

/// <summary>
/// Categorization of block types for pathfinding.
/// Mirror of net.minecraft.world.level.pathfinder.PathType
/// </summary>
public enum PathType
{
    Blocked = -1,
    Open = 0,
    Walkable = 1,
    WalkableDoor = 2,
    Trapdoor = 3,
    PowderSnow = 4,
    DangerPowderSnow = 5,
    Fence = 6,
    Lava = 7,
    Water = 8,
    WaterBorder = 9,
    Rail = 10,
    UnpassableRail = 11,
    DangerFire = 12,
    DamageFire = 13,
    DangerOther = 14,
    DamageOther = 15,
    DoorOpen = 16,
    DoorWoodClosed = 17,
    DoorIronClosed = 18,
    Breach = 19,
    Leaves = 20,
    StickyHoney = 21,
    Cocoa = 22,
    DamageCautious = 23,
    DangerTrapdoor = 24,
    WallNeighbor = 25
}

public static class PathTypeExtensions
{
    public static float GetMalus(this PathType type)
    {
        return type switch
        {
            PathType.Blocked => -1.0f,
            PathType.Open => 0.0f,
            PathType.Walkable => 0.0f,
            PathType.WalkableDoor => 0.0f,
            PathType.Trapdoor => 0.0f,
            PathType.PowderSnow => -1.0f,
            PathType.DangerPowderSnow => 0.0f,
            PathType.Fence => -1.0f,
            PathType.Lava => -1.0f,
            PathType.Water => 12.0f,
            PathType.WaterBorder => 5.0f,
            PathType.Rail => 0.0f,
            PathType.UnpassableRail => -1.0f,
            PathType.DangerFire => 8.0f,
            PathType.DamageFire => 16.0f,
            PathType.DangerOther => 8.0f,
            PathType.DamageOther => -1.0f,
            PathType.DoorOpen => 0.0f,
            PathType.DoorWoodClosed => -1.0f,
            PathType.DoorIronClosed => -1.0f,
            PathType.Breach => 4.0f,
            PathType.Leaves => -1.0f,
            PathType.StickyHoney => 8.0f,
            PathType.Cocoa => 0.0f,
            PathType.DamageCautious => 0.0f,
            PathType.DangerTrapdoor => 0.0f,
            PathType.WallNeighbor => 1.0f,
            _ => 0.0f
        };
    }
}
