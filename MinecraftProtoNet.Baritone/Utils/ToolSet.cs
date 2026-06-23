/*
 * This file is part of Baritone.
 *
 * Baritone is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Baritone is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with Baritone.  If not, see <https://www.gnu.org/licenses/>.
 *
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java
 */

using MinecraftProtoNet.Baritone.Core;
using MinecraftProtoNet.Core.Data;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;
using BaritoneSettings = MinecraftProtoNet.Baritone.Core.Baritone;

namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// A cached list of the best tools on the hotbar for any block.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java
/// </summary>
public class ToolSet
{
    /// <summary>
    /// A cache mapping a block name to how long it will take to break
    /// with this toolset, given the optimum tool is used.
    /// </summary>
    private readonly Dictionary<string, double> _breakStrengthCache = new();

    private readonly Entity _player;

    public ToolSet(Entity? player)
    {
        _player = player ?? new Entity();
    }

    /// <summary>
    /// Using the best tool on the hotbar, how fast we can mine this block.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:96-98
    /// </summary>
    public double GetStrVsBlock(BlockState state)
    {
        string blockName = state.Name;
        if (!_breakStrengthCache.TryGetValue(blockName, out double result))
        {
            result = GetBestDestructionTime(state);
            if (BaritoneSettings.Settings().ConsiderPotionEffects.Value)
            {
                double amplifier = PotionAmplifier();
                result = amplifier * result;
            }
            _breakStrengthCache[blockName] = result;
        }
        return result;
    }

    /// <summary>
    /// Calculate which tool on the hotbar is best for mining.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:135-182
    /// </summary>
    public int GetBestSlot(object block, bool preferSilkTouch)
    {
        return GetBestSlot(block, preferSilkTouch, false);
    }

    public int GetBestSlot(object block, bool preferSilkTouch, bool pathingCalculation)
    {
        var settings = BaritoneSettings.Settings();
        
        // If we actually want to know what efficiency our held item has instead of the best one
        // possible, this lets us make pathing depend on the actual tool to be used (if auto tool is disabled)
        if (!settings.AutoTool.Value && pathingCalculation)
        {
            return _player.HeldSlot;
        }

        int best = 0;
        double highestSpeed = double.NegativeInfinity;
        int lowestCost = int.MinValue; // Reference: ToolSet.java:124 (Integer.MIN_VALUE)
        bool bestSilkTouch = false;

        string blockName = block is string name ? name : block.ToString() ?? "";
        BlockState blockState = new BlockState(0, blockName, new Dictionary<string, string>());

        // Check hotbar slots (0-8, which map to container slots 36-44)
        for (int i = 0; i < 9; i++)
        {
            int containerSlot = i + 36;
            Slot itemStack = _player.Inventory.GetSlot((short)containerSlot);
            
            if (itemStack.ItemId == null || itemStack.ItemCount <= 0)
            {
                continue;
            }

            // Reference: ToolSet.java:129-131 - skip swords unless useSwordToMine
            string? slotItemName = null;
            if (itemStack.ItemId.HasValue)
            {
                ClientState.ItemRegistry?.TryGetValue(itemStack.ItemId.Value, out slotItemName);
            }
            if (!settings.UseSwordToMine.Value && !string.IsNullOrEmpty(slotItemName)
                && ToolData.GetToolType(slotItemName) == ToolData.ToolType.Sword)
            {
                continue;
            }

            // Reference: ToolSet.java:133 - itemSaver: skip near-broken tools.
            // (GetMaxDamage()==0 when the MaxDamage component isn't transmitted; the >1 guard makes that a safe no-op.)
            if (settings.ItemSaver.Value)
            {
                int maxDamage = itemStack.GetMaxDamage();
                if (maxDamage > 1 && (itemStack.GetDamageValue() + settings.ItemSaverThreshold.Value) >= maxDamage)
                {
                    continue;
                }
            }

            double speed = CalculateSpeedVsBlock(itemStack, blockState);
            bool silkTouch = HasSilkTouch(itemStack);
            
            if (speed > highestSpeed)
            {
                highestSpeed = speed;
                best = i;
                lowestCost = GetMaterialCost(itemStack);
                bestSilkTouch = silkTouch;
            }
            else if (speed == highestSpeed)
            {
                int cost = GetMaterialCost(itemStack);
                if ((cost < lowestCost && (silkTouch || !bestSilkTouch)) ||
                    (preferSilkTouch && !bestSilkTouch && silkTouch))
                {
                    highestSpeed = speed;
                    best = i;
                    lowestCost = cost;
                    bestSilkTouch = silkTouch;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Calculate how effectively a block can be destroyed.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:190-193
    /// </summary>
    private double GetBestDestructionTime(BlockState state)
    {
        int bestSlot = GetBestSlot(state.Name, false, true);
        int containerSlot = bestSlot + 36;
        Slot stack = _player.Inventory.GetSlot((short)containerSlot);
        return CalculateSpeedVsBlock(stack, state) * AvoidanceMultiplier(state);
    }

    private double AvoidanceMultiplier(BlockState state)
    {
        var settings = BaritoneSettings.Settings();
        return settings.BlocksToAvoidBreaking.Value.Contains(state.Name) 
            ? settings.AvoidBreakingMultiplier.Value 
            : 1.0;
    }

    /// <summary>
    /// Calculates how long would it take to mine the specified block given the best tool
    /// in this toolset is used. A negative value is returned if the specified block is unbreakable.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:207-239
    /// </summary>
    public static double CalculateSpeedVsBlock(Slot item, BlockState state)
    {
        // Get block hardness - for now use a simplified approach
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:208-214
        float hardness = GetBlockHardness(state);
        if (hardness < 0)
        {
            return -1;
        }

        // Instant-break blocks: any tool works equally, so return a fixed value
        // to prevent division by zero (Infinity) which breaks tool comparison
        if (hardness == 0.0f)
        {
            return IsEmpty(item) ? 0.0 : 1.0;
        }

        // Get item destroy speed
        float speed = GetItemDestroySpeed(item, state);
        if (speed > 1)
        {
            // Reference: ToolSet.java:193-198 - Efficiency bonus (eff^2 + 1) when the tool is effective
            int effLevel = EnchantmentHelper.GetItemEnchantmentLevel("minecraft:efficiency", item);
            if (effLevel > 0 && !IsEmpty(item))
            {
                speed += effLevel * effLevel + 1;
            }
        }

        speed /= hardness;
        
        // Check if correct tool for drops
        bool requiresCorrectTool = state.RequiresCorrectToolForDrops;
        bool isCorrectTool = IsCorrectToolForDrops(item, state);
        
        if (!requiresCorrectTool || (!IsEmpty(item) && isCorrectTool))
        {
            return speed / 30.0;
        }
        else
        {
            return speed / 100.0;
        }
    }

    /// <summary>
    /// Gets block hardness (destroy speed). Reference: ToolSet.java:183 — Java uses state.getDestroySpeed.
    /// Now backed by the real per-block hardness table (ClientState.BlockHardness) via BlockState.DestroySpeed,
    /// replacing the old name-substring approximation.
    /// </summary>
    private static float GetBlockHardness(BlockState state)
    {
        return state.DestroySpeed;
    }

    /// <summary>
    /// Gets item destroy speed against a block.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/Item.java:191-194
    /// Java: item.getDestroySpeed(state) → returns tool speed if effective, else 1.0
    /// </summary>
    private static float GetItemDestroySpeed(Slot item, BlockState state)
    {
        if (IsEmpty(item))
        {
            return 1.0f;
        }

        // Resolve item name from the protocol ID using the static item registry
        string? itemName = null;
        if (item.ItemId.HasValue)
        {
            ClientState.ItemRegistry?.TryGetValue(item.ItemId.Value, out itemName);
        }
        
        if (string.IsNullOrEmpty(itemName))
        {
            return 1.0f;
        }
        
        // Determine tool type and tier from item name
        var toolType = ToolData.GetToolType(itemName);
        if (toolType == ToolData.ToolType.None)
        {
            return 1.0f; // Not a tool
        }
        
        // Check if this tool type is effective against the target block
        if (!ToolData.IsCorrectTool(toolType, state))
        {
            return 1.0f; // Tool doesn't apply speed bonus to this block
        }
        
        // Return the tool tier's mining speed multiplier
        var tier = ToolData.GetToolTier(itemName);
        return ToolData.GetSpeed(tier);
    }

    /// <summary>
    /// Checks if item is correct tool for drops.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java:isCorrectToolForDrops
    /// </summary>
    private static bool IsCorrectToolForDrops(Slot item, BlockState state)
    {
        if (IsEmpty(item))
        {
            return false;
        }

        string? itemName = null;
        if (item.ItemId.HasValue)
        {
            ClientState.ItemRegistry?.TryGetValue(item.ItemId.Value, out itemName);
        }
        
        if (string.IsNullOrEmpty(itemName))
        {
            return false;
        }
        
        var toolType = ToolData.GetToolType(itemName);
        return toolType != ToolData.ToolType.None && ToolData.IsCorrectTool(toolType, state);
    }

    /// <summary>
    /// Checks if slot is empty.
    /// </summary>
    private static bool IsEmpty(Slot slot)
    {
        return slot.ItemId == null || slot.ItemId <= 0 || slot.ItemCount <= 0;
    }

    /// <summary>
    /// Evaluate the material cost of a possible tool.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:107-113
    /// </summary>
    /// <summary>
    /// Evaluate the material cost of a possible tool.
    /// Lower cost = prefer this tool (preserve expensive tools).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:107-113
    /// </summary>
    private int GetMaterialCost(Slot itemStack)
    {
        // Reference: ToolSet.java:87-94 - TieredItem → tier.getLevel(), otherwise -1.
        if (IsEmpty(itemStack))
        {
            return -1;
        }

        string? itemName = null;
        if (itemStack.ItemId.HasValue)
        {
            ClientState.ItemRegistry?.TryGetValue(itemStack.ItemId.Value, out itemName);
        }

        var tier = string.IsNullOrEmpty(itemName) ? ToolData.ToolTier.None : ToolData.GetToolTier(itemName);
        return tier == ToolData.ToolTier.None ? -1 : ToolData.GetHarvestLevel(tier);
    }

    /// <summary>
    /// Checks if item has silk touch enchantment.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:115-125
    /// </summary>
    public bool HasSilkTouch(Slot stack)
    {
        if (IsEmpty(stack))
        {
            return false;
        }

        // Reference: ToolSet.java:96-98 - getItemEnchantmentLevel(SILK_TOUCH, stack) > 0
        return EnchantmentHelper.GetItemEnchantmentLevel("minecraft:silk_touch", stack) > 0;
    }

    /// <summary>
    /// Calculates any modifier to breaking time based on status effects.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:246-268
    /// </summary>
    // Reference: ToolSet.java:213-235 - Haste (DIG_SPEED) speeds up, Mining Fatigue (DIG_SLOWDOWN) slows.
    private double PotionAmplifier()
    {
        double speed = 1.0;
        if (ClientState.MobEffectRegistry.TryGetValue("minecraft:haste", out var hasteId)
            && _player.GetEffectAmplifier(hasteId) is int hasteAmp)
        {
            speed *= 1 + (hasteAmp + 1) * 0.2;
        }
        if (ClientState.MobEffectRegistry.TryGetValue("minecraft:mining_fatigue", out var fatigueId)
            && _player.GetEffectAmplifier(fatigueId) is int fatigueAmp)
        {
            // Note: 0.0027 not 0.027 — see the (in)famous Java comment.
            speed *= fatigueAmp switch
            {
                0 => 0.3,
                1 => 0.09,
                2 => 0.0027,
                _ => 0.00081
            };
        }
        return speed;
    }
}

