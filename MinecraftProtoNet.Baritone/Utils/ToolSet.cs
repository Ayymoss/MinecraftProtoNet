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

    /// <summary>
    /// Used for evaluating the material cost of a tool.
    /// Prefer tools with lower material cost (lower index in this list).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:68-75
    /// </summary>
    private static readonly ToolData.ToolTier[] MaterialTagsPriorityList =
    {
        ToolData.ToolTier.Wood,
        ToolData.ToolTier.Stone,
        ToolData.ToolTier.Iron,
        ToolData.ToolTier.Gold,
        ToolData.ToolTier.Diamond,
        ToolData.ToolTier.Netherite
    };

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
        int lowestCost = int.MaxValue;
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

            // Check if item is a weapon and we shouldn't use sword to mine
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:156-158
            // For now, we'll skip this check as we don't have direct access to item components
            // TODO: Implement weapon check when component system is available

            // Check item saver setting
            if (settings.ItemSaver.Value)
            {
                // Get damage value from components if available
                // For now, skip this check as we need component access
                // TODO: Implement damage check when component system is available
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

        // Get item destroy speed
        float speed = GetItemDestroySpeed(item, state);
        if (speed > 1)
        {
            // Add efficiency enchantment bonus
            // TODO: Implement enchantment checking when component system is available
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:221-230
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
    /// Gets block hardness. Simplified implementation.
    /// </summary>
    private static float GetBlockHardness(BlockState state)
    {
        string name = state.Name;
        
        // Unbreakable blocks
        if (name.Contains("bedrock", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("command_block", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("barrier", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        // Very hard blocks
        if (name.Contains("obsidian", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("anvil", StringComparison.OrdinalIgnoreCase))
        {
            return 50.0f;
        }

        // Hard blocks
        if (name.Contains("stone", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ore", StringComparison.OrdinalIgnoreCase))
        {
            return 3.0f;
        }

        // Medium blocks
        if (name.Contains("dirt", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("wood", StringComparison.OrdinalIgnoreCase))
        {
            return 0.5f;
        }

        // Soft blocks
        if (name.Contains("air", StringComparison.OrdinalIgnoreCase))
        {
            return 0.0f;
        }

        // Default hardness
        return 1.0f;
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
        if (IsEmpty(itemStack))
        {
            return -1;
        }

        string? itemName = null;
        if (itemStack.ItemId.HasValue)
        {
            ClientState.ItemRegistry?.TryGetValue(itemStack.ItemId.Value, out itemName);
        }
        
        if (string.IsNullOrEmpty(itemName))
        {
            return int.MaxValue;
        }
        
        var tier = ToolData.GetToolTier(itemName);
        // Return index in priority list (lower = cheaper = preferred)
        for (int i = 0; i < MaterialTagsPriorityList.Length; i++)
        {
            if (MaterialTagsPriorityList[i] == tier) return i;
        }
        return int.MaxValue;
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

        // TODO: Check enchantments from components when available
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:116-124
        return false;
    }

    /// <summary>
    /// Calculates any modifier to breaking time based on status effects.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:246-268
    /// </summary>
    private double PotionAmplifier()
    {
        double speed = 1.0;
        
        // TODO: Check for Haste and Mining Fatigue effects when available
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/ToolSet.java:248-266
        // For now, return 1.0 (no effects)
        
        return speed;
    }
}

