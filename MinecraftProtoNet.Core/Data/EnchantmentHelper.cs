using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Data;

/// <summary>
/// Reads enchantment levels off items, mirroring net.minecraft.world.item.enchantment.EnchantmentHelper.
/// Enchant ids are resolved to holder indices via <see cref="ClientState.EnchantmentRegistry"/> (the
/// server-sent enchantment registry); levels come from an item's parsed Enchantments component
/// (ComponentType.Enchantments -> Map&lt;holderIndex,int level&gt;).
/// </summary>
public static class EnchantmentHelper
{
    // Player inventory (window 0) armor slots: 5=head, 6=chest, 7=legs, 8=feet.
    private const short ArmorSlotFirst = 5;
    private const short ArmorSlotLast = 8;

    /// <summary>
    /// The level of the given enchantment on a single item stack, or 0 if absent / registry not loaded.
    /// Reference: EnchantmentHelper.getItemEnchantmentLevel.
    /// </summary>
    public static int GetItemEnchantmentLevel(string enchantmentId, Slot? item)
    {
        if (item?.ComponentsToAdd == null) return 0;
        if (!ClientState.EnchantmentRegistry.TryGetValue(enchantmentId, out var holderIndex)) return 0;
        foreach (var component in item.ComponentsToAdd)
        {
            if (component.Type == ComponentType.Enchantments && component.Data is Dictionary<int, int> levels)
            {
                return levels.GetValueOrDefault(holderIndex, 0);
            }
        }
        return 0;
    }

    /// <summary>
    /// The highest level of the given enchantment across the player's worn armor (used for
    /// frost walker / depth strider, which are boots enchantments).
    /// Reference: EnchantmentHelper.getEnchantmentLevel(enchant, livingEntity) over equipment.
    /// </summary>
    public static int GetArmorEnchantmentLevel(string enchantmentId, Entity player)
    {
        int max = 0;
        for (short slot = ArmorSlotFirst; slot <= ArmorSlotLast; slot++)
        {
            int level = GetItemEnchantmentLevel(enchantmentId, player.Inventory.GetSlot(slot));
            if (level > max) max = level;
        }
        return max;
    }
}
