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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Cache;
using MinecraftProtoNet.Baritone.Pathfinding.Precompute;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Baritone.Utils.Pathing;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.State;
using BaritoneSettings = MinecraftProtoNet.Baritone.Core.Baritone;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// Context for pathfinding calculations.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java
/// </summary>
public class CalculationContext
{
    public readonly bool SafeForThreadedUse;
    public readonly IBaritone Baritone;
    public readonly Level World;
    public readonly WorldData WorldData;
    public readonly BlockStateInterface Bsi;
    public readonly ToolSet ToolSet;
    public readonly bool HasWaterBucket;
    public readonly bool HasThrowaway;
    public readonly bool CanSprint;
    protected readonly double PlaceBlockCost;
    public readonly bool AllowBreak;
    public readonly List<object> AllowBreakAnyway; // Note: In Java this is List<Block>, but in C# we use object for flexibility
    public readonly bool AllowParkour;
    public readonly bool AllowParkourPlace;
    public readonly bool AllowJumpAtBuildLimit;
    public readonly bool AllowParkourAscend;
    public readonly bool AssumeWalkOnWater;
    public bool AllowFallIntoLava;
    public readonly int FrostWalker;
    public readonly bool AllowDiagonalDescend;
    public readonly bool AllowDiagonalAscend;
    public readonly bool AllowDownward;
    public int MinFallHeight;
    public int MaxFallHeightNoWater;
    public readonly int MaxFallHeightBucket;
    public readonly double WaterWalkSpeed;
    public readonly double BreakBlockAdditionalCost;
    public double BacktrackCostFavoringCoefficient;
    public double JumpPenalty;
    public readonly double WalkOnWaterOnePenalty;
    public readonly BetterWorldBorder WorldBorder;

    public readonly PrecomputedData PrecomputedData;

    public CalculationContext(IBaritone baritone) : this(baritone, false)
    {
    }

    public CalculationContext(IBaritone baritone, bool forUseOnAnotherThread)
    {
        PrecomputedData = new PrecomputedData();
        SafeForThreadedUse = forUseOnAnotherThread;
        Baritone = baritone;
        var player = baritone.GetPlayerContext().Player();
        World = (Level)baritone.GetPlayerContext().World()!;
        WorldData = (WorldData)baritone.GetWorldProvider().GetCurrentWorld()!;
        Bsi = new BlockStateInterface(baritone.GetPlayerContext(), forUseOnAnotherThread);
        ToolSet = new ToolSet((Entity?)player);
        HasThrowaway = BaritoneSettings.Settings().AllowPlace.Value && baritone.GetInventoryBehavior().HasGenericThrowaway();
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java:103
        // Check for water bucket in hotbar
        HasWaterBucket = false;
        if (BaritoneSettings.Settings().AllowWaterBucketFall.Value && player is Entity entity)
        {
            var itemRegistry = baritone.GetItemRegistryService();
            // Check hotbar slots (36-44) for water bucket
            for (int i = 36; i <= 44; i++)
            {
                var slot = entity.Inventory.GetSlot((short)i);
                if (slot.ItemId != null && slot.ItemCount > 0)
                {
                    var itemName = itemRegistry.GetItemName(slot.ItemId.Value);
                    if (itemName != null && (itemName.Contains("water_bucket", StringComparison.OrdinalIgnoreCase) ||
                        itemName.Equals("minecraft:water_bucket", StringComparison.OrdinalIgnoreCase)))
                    {
                        HasWaterBucket = true;
                        break;
                    }
                }
            }
            // Also check dimension - water bucket not useful in nether
            if (HasWaterBucket && World.DimensionType.Name.Contains("nether", StringComparison.OrdinalIgnoreCase))
            {
                HasWaterBucket = false;
            }
        }
        CanSprint = BaritoneSettings.Settings().AllowSprint.Value && (player as Entity)?.Hunger > 6;
        PlaceBlockCost = BaritoneSettings.Settings().BlockPlacementPenalty.Value;
        AllowBreak = BaritoneSettings.Settings().AllowBreak.Value;
        AllowBreakAnyway = new List<object>(BaritoneSettings.Settings().AllowBreakAnyway.Value);
        AllowParkour = BaritoneSettings.Settings().AllowParkour.Value;
        AllowParkourPlace = BaritoneSettings.Settings().AllowParkourPlace.Value;
        AllowJumpAtBuildLimit = BaritoneSettings.Settings().AllowJumpAtBuildLimit.Value;
        AllowParkourAscend = BaritoneSettings.Settings().AllowParkourAscend.Value;
        AssumeWalkOnWater = BaritoneSettings.Settings().AssumeWalkOnWater.Value;
        AllowFallIntoLava = false;
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java:115-127
        // Check for Frost Walker enchantment on equipment
        int frostWalkerLevel = 0;
        if (player is Entity playerEntity)
        {
            // Check all equipment slots for Frost Walker
            // Equipment slots: 36-39 (main hand, off hand, boots, leggings, chestplate, helmet)
            // In Minecraft, equipment slots are: MAINHAND(0), OFFHAND(40), FEET(36), LEGS(37), CHEST(38), HEAD(39)
            int[] equipmentSlots = { 36, 37, 38, 39, 40 }; // FEET, LEGS, CHEST, HEAD, OFFHAND
            foreach (int slotIndex in equipmentSlots)
            {
                var slot = playerEntity.Inventory.GetSlot((short)slotIndex);
                if (slot.ItemId != null && slot.ItemCount > 0)
                {
                    // Check for Frost Walker enchantment in slot components
                    // Reference: minecraft-26.1-REFERENCE-ONLY - Enchantments are stored in ComponentType.Enchantments (13)
                    // For now, we'll check if the slot has enchantment data
                    // TODO: When component system is fully available, parse enchantments and check for FROST_WALKER
                    // This is a simplified check - full implementation would parse ItemEnchantments component
                    // Note: Slot.Components property doesn't exist yet - this is a placeholder
                    // if (slot.Components != null)
                    // {
                    //     // Check if enchantments component exists (ComponentType.Enchantments = 13)
                    //     // Full implementation would iterate through enchantments and find FROST_WALKER level
                    //     // For now, we'll leave this as 0 until component parsing is available
                    // }
                }
            }
        }
        FrostWalker = frostWalkerLevel;
        AllowDiagonalDescend = BaritoneSettings.Settings().AllowDiagonalDescend.Value;
        AllowDiagonalAscend = BaritoneSettings.Settings().AllowDiagonalAscend.Value;
        AllowDownward = BaritoneSettings.Settings().AllowDownward.Value;
        MinFallHeight = 3;
        MaxFallHeightNoWater = BaritoneSettings.Settings().MaxFallHeightNoWater.Value;
        MaxFallHeightBucket = BaritoneSettings.Settings().MaxFallHeightBucket.Value;
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java:134-151
        // Calculate water walk speed based on Depth Strider enchantment (water movement efficiency)
        float waterSpeedMultiplier = 1.0f;
        if (player is Entity playerEntity2)
        {
            // Check equipment slots for Depth Strider
            // Equipment slots: 36-39 (boots, leggings, chestplate, helmet)
            int[] equipmentSlots = { 36, 37, 38, 39, 40 }; // FEET, LEGS, CHEST, HEAD, OFFHAND
            foreach (int slotIndex in equipmentSlots)
            {
                var slot = playerEntity2.Inventory.GetSlot((short)slotIndex);
                if (slot.ItemId != null && slot.ItemCount > 0)
                {
                    // Check for Depth Strider (water movement efficiency) enchantment
                    // Reference: minecraft-26.1-REFERENCE-ONLY - Enchantments are stored in ComponentType.Enchantments (13)
                    // The Java version checks for EnchantmentEffectComponents.ATTRIBUTES with WATER_MOVEMENT_EFFICIENCY
                    // TODO: When component system is fully available, parse enchantments and check for WATER_MOVEMENT_EFFICIENCY
                    // This is a simplified check - full implementation would parse ItemEnchantments and EnchantmentAttributeEffect
                    // Note: Slot.Components property doesn't exist yet - this is a placeholder
                    // Full implementation would:
                    // 1. Get ItemEnchantments from slot.Components[ComponentType.Enchantments]
                    // 2. Iterate through enchantments
                    // 3. Get EnchantmentEffectComponents.ATTRIBUTES for each enchantment
                    // 4. Check if attribute is WATER_MOVEMENT_EFFICIENCY
                    // 5. Calculate multiplier: effect.amount().calculate(enchantmentLevel)
                    // For now, we'll leave multiplier at 1.0 until component parsing is available
                }
            }
        }
        WaterWalkSpeed = ActionCosts.WalkOneInWaterCost * (1 - waterSpeedMultiplier) + ActionCosts.WalkOneBlockCost * waterSpeedMultiplier;
        BreakBlockAdditionalCost = BaritoneSettings.Settings().BlockBreakAdditionalPenalty.Value;
        BacktrackCostFavoringCoefficient = BaritoneSettings.Settings().BacktrackCostFavoringCoefficient.Value;
        JumpPenalty = BaritoneSettings.Settings().JumpPenalty.Value;
        WalkOnWaterOnePenalty = BaritoneSettings.Settings().WalkOnWaterOnePenalty.Value;
        WorldBorder = new BetterWorldBorder(World.WorldBorder);
    }

    public IBaritone GetBaritone() => Baritone;

    public BlockState Get(int x, int y, int z)
    {
        return Bsi.Get0(x, y, z);
    }

    public bool IsLoaded(int x, int z)
    {
        return Bsi.IsLoaded(x, z);
    }

    public virtual double CostOfPlacingAt(int x, int y, int z, BlockState current)
    {
        if (!HasThrowaway)
        {
            return ActionCosts.CostInf;
        }
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java:186-198
        if (IsPossiblyProtected(x, y, z))
        {
            return ActionCosts.CostInf;
        }
        if (!WorldBorder.CanPlaceAt(x, z))
        {
            return ActionCosts.CostInf;
        }
        // Check fluid state
        if (!BaritoneSettings.Settings().AllowPlaceInFluidsSource.Value && current.IsLiquid && current.Properties.TryGetValue("level", out var levelStr) && levelStr == "0")
        {
            return ActionCosts.CostInf;
        }
        if (!BaritoneSettings.Settings().AllowPlaceInFluidsFlow.Value && current.IsLiquid && !(current.Properties.TryGetValue("level", out var levelStr2) && levelStr2 == "0"))
        {
            return ActionCosts.CostInf;
        }
        return PlaceBlockCost;
    }

    public virtual double BreakCostMultiplierAt(int x, int y, int z, BlockState state)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java:201-209
        if (!AllowBreak && !AllowBreakAnyway.Contains(state.Name))
        {
            return ActionCosts.CostInf;
        }
        if (IsPossiblyProtected(x, y, z))
        {
            return ActionCosts.CostInf;
        }
        return 1.0;
    }

    public object GetBlock(int x, int y, int z)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java:203
        // Returns block name (string representation)
        return Get(x, y, z).Name;
    }

    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/CalculationContext.java:211-213
    public virtual double PlaceBucketCost()
    {
        return PlaceBlockCost;
    }

    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/CalculationContext.java:215-218
    public bool IsPossiblyProtected(int x, int y, int z)
    {
        // TODO: More protection logic here; see #220
        // For now, return false (no protection checks)
        return false;
    }
}

