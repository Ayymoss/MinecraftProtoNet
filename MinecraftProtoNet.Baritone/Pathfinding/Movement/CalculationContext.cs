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
    public readonly List<object> AllowBreakAnyway;
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

    private static bool? _cachedHasThrowaway;
    private static bool? _cachedHasWaterBucket;
    private static int _lastCachedTick = -1;

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
        var currentTick = World.ClientTickCounter;
        
        if (!forUseOnAnotherThread && currentTick != _lastCachedTick)
        {
            _cachedHasThrowaway = null;
            _cachedHasWaterBucket = null;
            _lastCachedTick = (int)currentTick;
        }

        Bsi = new BlockStateInterface(baritone.GetPlayerContext(), forUseOnAnotherThread);
        ToolSet = new ToolSet((Entity?)player);
        
        if (forUseOnAnotherThread)
        {
            HasThrowaway = BaritoneSettings.Settings().AllowPlace.Value && baritone.GetInventoryBehavior().HasGenericThrowaway();
        }
        else
        {
            HasThrowaway = _cachedHasThrowaway ??= BaritoneSettings.Settings().AllowPlace.Value && baritone.GetInventoryBehavior().HasGenericThrowaway();
        }
        //Baritone.GetGameEventHandler().LogDirect($"[DEBUG] CalculationContext: HasThrowaway={HasThrowaway}, AllowPlace={BaritoneSettings.Settings().AllowPlace.Value}");

        if (forUseOnAnotherThread)
        {
            HasWaterBucket = CheckWaterBucket(baritone, player);
        }
        else
        {
            HasWaterBucket = _cachedHasWaterBucket ??= CheckWaterBucket(baritone, player);
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

        int frostWalkerLevel = 0;
        if (player is Entity playerEntity)
        {
            int[] equipmentSlots = { 36, 37, 38, 39, 40 };
            foreach (int slotIndex in equipmentSlots)
            {
                var slot = playerEntity.Inventory.GetSlot((short)slotIndex);
                if (slot.ItemId != null && slot.ItemCount > 0)
                {
                    // Enchantment checking logic placeholder
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

        float waterSpeedMultiplier = 1.0f;
        if (player is Entity playerEntity2)
        {
            int[] equipmentSlots = { 36, 37, 38, 39, 40 };
            foreach (int slotIndex in equipmentSlots)
            {
                var slot = playerEntity2.Inventory.GetSlot((short)slotIndex);
                if (slot.ItemId != null && slot.ItemCount > 0)
                {
                    // Depth Strider checking logic placeholder
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

    private static bool CheckWaterBucket(IBaritone baritone, object? player)
    {
        if (BaritoneSettings.Settings().AllowWaterBucketFall.Value && player is Entity entity)
        {
            var itemRegistry = baritone.GetItemRegistryService();
            for (int i = 36; i <= 44; i++)
            {
                var slot = entity.Inventory.GetSlot((short)i);
                if (slot.ItemId != null && slot.ItemCount > 0)
                {
                    var itemName = itemRegistry.GetItemName(slot.ItemId.Value);
                    if (itemName != null && (itemName.Contains("water_bucket", StringComparison.OrdinalIgnoreCase) ||
                        itemName.Equals("minecraft:water_bucket", StringComparison.OrdinalIgnoreCase)))
                    {
                        var world = baritone.GetPlayerContext().World() as Level;
                        if (world != null && world.DimensionType.Name.Contains("nether", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                        return true;
                    }
                }
            }
        }
        return false;
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
        if (IsPossiblyProtected(x, y, z))
        {
            return ActionCosts.CostInf;
        }
        if (!WorldBorder.CanPlaceAt(x, z))
        {
            return ActionCosts.CostInf;
        }
        if (!BaritoneSettings.Settings().AllowPlaceInFluidsSource.Value && current.IsLiquid && (!current.Properties.TryGetValue("level", out var levelStr) || levelStr == "0"))
        {
            return ActionCosts.CostInf;
        }
        if (!BaritoneSettings.Settings().AllowPlaceInFluidsFlow.Value && current.IsLiquid && current.Properties.TryGetValue("level", out var levelStr2) && levelStr2 != "0")
        {
            return ActionCosts.CostInf;
        }
        return PlaceBlockCost;
    }

    public virtual double BreakCostMultiplierAt(int x, int y, int z, BlockState state)
    {
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
        return Get(x, y, z).Name;
    }

    public virtual double PlaceBucketCost()
    {
        return PlaceBlockCost;
    }

    public bool IsPossiblyProtected(int x, int y, int z)
    {
        return false;
    }
}
