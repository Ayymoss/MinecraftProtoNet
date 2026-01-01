namespace MinecraftProtoNet.Enums;

/// <summary>
/// Menu types from Minecraft's registry, determining the UI layout for containers.
/// Order matches minecraft:menu registry.
/// </summary>
public enum MenuType
{
    /// <summary>ChestMenu - 1 row (9 slots)</summary>
    Generic9x1 = 0,
    
    /// <summary>ChestMenu - 2 rows (18 slots)</summary>
    Generic9x2 = 1,
    
    /// <summary>ChestMenu - 3 rows (27 slots, single chest)</summary>
    Generic9x3 = 2,
    
    /// <summary>ChestMenu - 4 rows (36 slots)</summary>
    Generic9x4 = 3,
    
    /// <summary>ChestMenu - 5 rows (45 slots)</summary>
    Generic9x5 = 4,
    
    /// <summary>ChestMenu - 6 rows (54 slots, double chest)</summary>
    Generic9x6 = 5,
    
    /// <summary>DispenserMenu - 3x3 grid (9 slots)</summary>
    Generic3x3 = 6,
    
    /// <summary>CrafterMenu - 3x3 crafter grid</summary>
    Crafter3x3 = 7,
    
    /// <summary>AnvilMenu - repair/rename items</summary>
    Anvil = 8,
    
    /// <summary>BeaconMenu - beacon configuration</summary>
    Beacon = 9,
    
    /// <summary>BlastFurnaceMenu - ore smelting</summary>
    BlastFurnace = 10,
    
    /// <summary>BrewingStandMenu - potion brewing</summary>
    BrewingStand = 11,
    
    /// <summary>CraftingMenu - 3x3 crafting table</summary>
    Crafting = 12,
    
    /// <summary>EnchantmentMenu - enchanting table</summary>
    Enchantment = 13,
    
    /// <summary>FurnaceMenu - smelting</summary>
    Furnace = 14,
    
    /// <summary>GrindstoneMenu - disenchanting/repair</summary>
    Grindstone = 15,
    
    /// <summary>HopperMenu - 5 slots horizontal</summary>
    Hopper = 16,
    
    /// <summary>LecternMenu - book reading</summary>
    Lectern = 17,
    
    /// <summary>LoomMenu - banner patterns</summary>
    Loom = 18,
    
    /// <summary>MerchantMenu - villager/wandering trader</summary>
    Merchant = 19,
    
    /// <summary>ShulkerBoxMenu - portable storage</summary>
    ShulkerBox = 20,
    
    /// <summary>SmithingMenu - netherite upgrades/trims</summary>
    Smithing = 21,
    
    /// <summary>SmokerMenu - food cooking</summary>
    Smoker = 22,
    
    /// <summary>CartographyTableMenu - map editing</summary>
    CartographyTable = 23,
    
    /// <summary>StonecutterMenu - stone variants</summary>
    Stonecutter = 24
}

/// <summary>
/// Extension methods for MenuType.
/// </summary>
public static class MenuTypeExtensions
{
    /// <summary>
    /// Gets the number of container-specific slots for this menu type (excluding player inventory).
    /// </summary>
    public static int GetContainerSlotCount(this MenuType type) => type switch
    {
        MenuType.Generic9x1 => 9,
        MenuType.Generic9x2 => 18,
        MenuType.Generic9x3 => 27,
        MenuType.Generic9x4 => 36,
        MenuType.Generic9x5 => 45,
        MenuType.Generic9x6 => 54,
        MenuType.Generic3x3 => 9,
        MenuType.Crafter3x3 => 9,
        MenuType.Anvil => 3,
        MenuType.Beacon => 1,
        MenuType.BlastFurnace => 3,
        MenuType.BrewingStand => 5,
        MenuType.Crafting => 10, // 9 grid + 1 result
        MenuType.Enchantment => 2,
        MenuType.Furnace => 3,
        MenuType.Grindstone => 3,
        MenuType.Hopper => 5,
        MenuType.Lectern => 1,
        MenuType.Loom => 4,
        MenuType.Merchant => 3,
        MenuType.ShulkerBox => 27,
        MenuType.Smithing => 4,
        MenuType.Smoker => 3,
        MenuType.CartographyTable => 3,
        MenuType.Stonecutter => 2,
        _ => 0
    };

    /// <summary>
    /// Gets the number of columns for grid-based container display.
    /// </summary>
    public static int GetColumnCount(this MenuType type) => type switch
    {
        MenuType.Generic9x1 or MenuType.Generic9x2 or MenuType.Generic9x3 
            or MenuType.Generic9x4 or MenuType.Generic9x5 or MenuType.Generic9x6 
            or MenuType.ShulkerBox => 9,
        MenuType.Generic3x3 or MenuType.Crafter3x3 or MenuType.Crafting => 3,
        MenuType.Hopper => 5,
        _ => 3 // Default for special menus
    };
}
