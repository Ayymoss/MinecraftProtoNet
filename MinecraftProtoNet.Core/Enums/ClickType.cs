using MinecraftProtoNet.Core.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Core.Enums;

/// <summary>
/// Represents the type of inventory click action.
/// Equivalent to Java's net.minecraft.world.inventory.ClickType.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerController.java:70
/// Used by Baritone for inventory management.
/// </summary>
public enum ClickType
{
    /// <summary>
    /// Pickup item from slot (left click pickup, right click half pickup).
    /// Equivalent to Java's ClickType.PICKUP.
    /// </summary>
    Pickup = 0,

    /// <summary>
    /// Quick move item to opposite inventory (shift+click).
    /// Equivalent to Java's ClickType.QUICK_MOVE.
    /// </summary>
    QuickMove = 1,

    /// <summary>
    /// Swap item with hotbar slot (number key click).
    /// Equivalent to Java's ClickType.SWAP.
    /// </summary>
    Swap = 2,

    /// <summary>
    /// Clone item in creative mode (middle click).
    /// Equivalent to Java's ClickType.CLONE.
    /// </summary>
    Clone = 3,

    /// <summary>
    /// Throw item out of inventory (Q key, or drag outside).
    /// Equivalent to Java's ClickType.THROW.
    /// </summary>
    Throw = 4,

    /// <summary>
    /// Quick craft action (shift+click in crafting grid).
    /// Equivalent to Java's ClickType.QUICK_CRAFT.
    /// </summary>
    QuickCraft = 5,

    /// <summary>
    /// Pickup all items from slot (double click).
    /// Equivalent to Java's ClickType.PICKUP_ALL.
    /// </summary>
    PickupAll = 6
}

/// <summary>
/// Extension methods for converting ClickType to ClickContainerMode.
/// </summary>
public static class ClickTypeExtensions
{
    /// <summary>
    /// Converts ClickType to ClickContainerMode.
    /// The enum values match exactly, so this is a simple cast.
    /// </summary>
    public static ClickContainerMode ToClickContainerMode(this ClickType clickType)
    {
        return (ClickContainerMode)(int)clickType;
    }
}

