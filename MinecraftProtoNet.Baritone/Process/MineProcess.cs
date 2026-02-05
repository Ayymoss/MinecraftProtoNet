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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/MineProcess.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Cache;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.State;
using BaritoneSettings = MinecraftProtoNet.Baritone.Core.Baritone;

// Runnable is in Baritone.Core namespace (same file as Baritone class)

namespace MinecraftProtoNet.Baritone.Process;

/// <summary>
/// Mine process implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/MineProcess.java
/// </summary>
public class MineProcess : BaritoneProcessHelper, IMineProcess
{
    private BlockOptionalMetaLookup? _filter;
    private List<BetterBlockPos> _knownOreLocations = new();
    private List<BetterBlockPos> _blacklist = new(); // inaccessible
    private Dictionary<BetterBlockPos, long> _anticipatedDrops = new();
    private BetterBlockPos? _branchPoint;
    private GoalRunAway? _branchPointRunaway;
    private int _desiredQuantity;
    private int _tickCount;

    public MineProcess(IBaritone baritone) : base(baritone)
    {
    }

    public override bool IsActive() => _filter != null;

    public override PathingCommand OnTick(bool calcFailed, bool isSafeToCancel)
    {
        if (_desiredQuantity > 0)
        {
            var player = Ctx.Player();
            if (player != null)
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/MineProcess.java:65-68
                // Check inventory for desired quantity
                int curr = 0;
                if (_filter != null && player is Entity entity)
                {
                    // Count items matching filter in inventory
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/MineProcess.java:65-68
                    var itemRegistry = Baritone.GetItemRegistryService();
                    for (int i = 0; i < 36; i++) // Main inventory + hotbar
                    {
                        var slot = entity.Inventory.GetSlot((short)i);
                        if (slot.ItemId != null && slot.ItemCount > 0)
                        {
                            var itemName = itemRegistry.GetItemName(slot.ItemId.Value);
                            if (itemName != null && _filter != null && _filter.Has(itemName))
                            {
                                curr += slot.ItemCount;
                            }
                        }
                    }
                }
                if (curr >= _desiredQuantity)
                {
                    LogDirect("Have " + curr + " valid items");
                    Cancel();
                    return new PathingCommand(null, PathingCommandType.RequestPause);
                }
            }
        }
        if (calcFailed)
        {
            if (_knownOreLocations.Any() && BaritoneSettings.Settings().BlacklistClosestOnFailure.Value)
            {
                LogDirect("Unable to find any path to " + _filter + ", blacklisting presumably unreachable closest instance...");
                if (BaritoneSettings.Settings().NotificationOnMineFail.Value)
                {
                    LogNotification("Unable to find any path to " + _filter + ", blacklisting presumably unreachable closest instance...", true);
                }
                _knownOreLocations.MinBy(pos => Ctx.PlayerFeet()?.DistanceSq(pos) ?? double.MaxValue)?.Let(_blacklist.Add);
                _knownOreLocations.RemoveAll(_blacklist.Contains);
            }
            else
            {
                LogDirect("Unable to find any path to " + _filter + ", canceling mine");
                if (BaritoneSettings.Settings().NotificationOnMineFail.Value)
                {
                    LogNotification("Unable to find any path to " + _filter + ", canceling mine", true);
                }
                Cancel();
                return new PathingCommand(null, PathingCommandType.RequestPause);
            }
        }

        UpdateLoucaSystem();
        int mineGoalUpdateInterval = BaritoneSettings.Settings().MineGoalUpdateInterval.Value;
        List<BetterBlockPos> currLocs = new(_knownOreLocations);
        if (mineGoalUpdateInterval != 0 && _tickCount++ % mineGoalUpdateInterval == 0)
        {
            CalculationContext context = new(Baritone, true);
            MinecraftProtoNet.Baritone.Core.Baritone.GetExecutor().Execute(new ActionRunnable(() => Rescan(currLocs, context)));
        }
        if (BaritoneSettings.Settings().LegitMine.Value)
        {
            if (!AddNearby())
            {
                Cancel();
                return new PathingCommand(null, PathingCommandType.RequestPause);
            }
        }
        var shaft = currLocs.Where(pos => pos.X == Ctx.PlayerFeet()?.X && pos.Z == Ctx.PlayerFeet()?.Z)
                            .Where(pos => pos.Y >= (Ctx.PlayerFeet()?.Y ?? 0))
                            .Where(pos => !BlockStateInterface.Get(Ctx, pos).IsAir)
                            .MinBy(pos => Ctx.PlayerFeet()?.Above().DistanceSq(pos) ?? double.MaxValue);
        Baritone.GetInputOverrideHandler().ClearAllKeys();
        if (shaft != null && (Ctx.Player() as Entity)?.IsOnGround == true)
        {
            var pos = shaft;
            BlockState state = ((Core.Baritone)Baritone).Bsi!.Get0(pos.X, pos.Y, pos.Z);
            if (!MovementHelper.AvoidBreaking(((Core.Baritone)Baritone).Bsi!, pos.X, pos.Y, pos.Z, state))
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/MineProcess.java:129-140
                var rot = Utils.RotationUtils.Reachable(Ctx, pos);
                if (rot != null && isSafeToCancel)
                {
                    Baritone.GetLookBehavior().UpdateTarget(rot, true);
                    var world = Ctx.World() as Level;
                    if (world != null)
                    {
                        var blockState = world.GetBlockAt(pos.X, pos.Y, pos.Z);
                        if (blockState != null)
                        {
                            MovementHelper.SwitchToBestToolFor(Ctx, blockState);
                        }
                    }
                    var playerRot = Ctx.PlayerRotations();
                    if (playerRot != null && (Ctx.IsLookingAt(pos) || playerRot.IsReallyCloseTo(rot)))
                    {
                        Baritone.GetInputOverrideHandler().SetInputForceState(Api.Utils.Input.Input.ClickLeft, true);
                    }
                    return new PathingCommand(null, PathingCommandType.RequestPause);
                }
            }
        }
        PathingCommand? command = UpdateGoal();
        if (command == null)
        {
            Cancel();
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }
        return command;
    }

    private void UpdateLoucaSystem()
    {
        Dictionary<BetterBlockPos, long> copy = new(_anticipatedDrops);
        Ctx.GetSelectedBlock()?.Let(pos =>
        {
            if (_knownOreLocations.Contains(pos))
            {
                copy[pos] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Core.Baritone.Settings().MineDropLoiterDurationMsThanksLouca.Value;
            }
        });
        foreach (var entry in _anticipatedDrops)
        {
            if (copy.TryGetValue(entry.Key, out long timestamp) && timestamp < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            {
                copy.Remove(entry.Key);
            }
        }
        _anticipatedDrops = copy;
    }

    public override void OnLostControl()
    {
        Mine(0, (BlockOptionalMetaLookup?)null);
    }

    public override string DisplayName()
    {
        return "Mine " + _filter;
    }

    private PathingCommand? UpdateGoal()
    {
        BlockOptionalMetaLookup? filter = FilterFilter();
        if (filter == null)
        {
            return null;
        }

        bool legit = BaritoneSettings.Settings().LegitMine.Value;
        List<BetterBlockPos> locs = _knownOreLocations;
        if (locs.Any())
        {
            CalculationContext context = new(Baritone);
            List<BetterBlockPos> locs2 = Prune(context, new List<BetterBlockPos>(locs), filter, BaritoneSettings.Settings().MineMaxOreLocationsCount.Value, _blacklist, DroppedItemsScan());
            Goal goal = new GoalComposite(locs2.Select(loc => Coalesce(loc, locs2, context)).ToArray());
            _knownOreLocations = locs2;
            return new PathingCommand(goal, legit ? PathingCommandType.ForceRevalidateGoalAndPath : PathingCommandType.RevalidateGoalAndPath);
        }
        if (!legit && !BaritoneSettings.Settings().ExploreForBlocks.Value)
        {
            return null;
        }
        int y = BaritoneSettings.Settings().LegitMineYLevel.Value;
        if (_branchPoint == null)
        {
            _branchPoint = Ctx.PlayerFeet();
        }
        if (_branchPointRunaway == null)
        {
            // Create a custom GoalRunAway that always returns false for IsInGoal and negative infinity for Heuristic
            // This is used to force pathfinding away from the branch point
            if (_branchPoint != null)
            {
                _branchPointRunaway = new GoalRunAwayOverride(1, y, _branchPoint);
            }
        }
        return new PathingCommand(_branchPointRunaway, PathingCommandType.RevalidateGoalAndPath);
    }

    private void Rescan(List<BetterBlockPos> already, CalculationContext context)
    {
        BlockOptionalMetaLookup? filter = FilterFilter();
        if (filter == null)
        {
            return;
        }
        if (BaritoneSettings.Settings().LegitMine.Value)
        {
            return;
        }
        List<BetterBlockPos> dropped = DroppedItemsScan();
        List<BetterBlockPos> locs = SearchWorld(context, filter, BaritoneSettings.Settings().MineMaxOreLocationsCount.Value, already, _blacklist, dropped);
        locs.AddRange(dropped);
        if (!locs.Any() && !BaritoneSettings.Settings().ExploreForBlocks.Value)
        {
            LogDirect("No locations for " + filter + " known, cancelling");
                if (BaritoneSettings.Settings().NotificationOnMineFail.Value)
            {
                LogNotification("No locations for " + filter + " known, cancelling", true);
            }
            Cancel();
            return;
        }
        _knownOreLocations = locs;
    }

    private bool InternalMiningGoal(BetterBlockPos pos, CalculationContext context, List<BetterBlockPos> locs)
    {
        if (locs.Contains(pos))
        {
            return true;
        }
        BlockState state = context.Bsi.Get0(pos.X, pos.Y, pos.Z);
        if (BaritoneSettings.Settings().InternalMiningAirException.Value && state.IsAir)
        {
            return true;
        }
        return _filter!.Has(state) && PlausibleToBreak(context, pos);
    }

    private Goal Coalesce(BetterBlockPos loc, List<BetterBlockPos> locs, CalculationContext context)
    {
        // TODO: FallingBlock
        var aboveState = context.Bsi.Get0(loc.Above().X, loc.Above().Y, loc.Above().Z);
        bool assumeVerticalShaftMine = !(aboveState.Name.Contains("gravel", StringComparison.OrdinalIgnoreCase) || aboveState.Name.Contains("sand", StringComparison.OrdinalIgnoreCase));
        if (!BaritoneSettings.Settings().ForceInternalMining.Value)
        {
            if (assumeVerticalShaftMine)
            {
                return new GoalThreeBlocks(loc);
            }
            else
            {
                return new GoalTwoBlocks(loc);
            }
        }
        bool upwardGoal = InternalMiningGoal(loc.Above(), context, locs);
        bool downwardGoal = InternalMiningGoal(loc.Below(), context, locs);
        bool doubleDownwardGoal = InternalMiningGoal(loc.Below(2), context, locs);
        if (upwardGoal == downwardGoal)
        {
            if (doubleDownwardGoal && assumeVerticalShaftMine)
            {
                return new GoalThreeBlocks(loc);
            }
            else
            {
                return new GoalTwoBlocks(loc);
            }
        }
        if (upwardGoal)
        {
            return new GoalBlock(loc);
        }
        if (doubleDownwardGoal && assumeVerticalShaftMine)
        {
            return new GoalTwoBlocks(loc.Below());
        }
        return new GoalBlock(loc.Below());
    }

    private class GoalThreeBlocks : GoalTwoBlocks
    {
        public GoalThreeBlocks(BetterBlockPos pos) : base(pos)
        {
        }

        public override bool IsInGoal(int x, int y, int z)
        {
            return x == X && (y == Y || y == Y - 1 || y == Y - 2) && z == Z;
        }

        public override double Heuristic(int x, int y, int z)
        {
            int xDiff = x - X;
            int yDiff = y - Y;
            int zDiff = z - Z;
            return GoalBlock.Calculate(xDiff, yDiff < -1 ? yDiff + 2 : yDiff == -1 ? 0 : yDiff, zDiff);
        }

        public override bool Equals(object? o)
        {
            return base.Equals(o);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() * 393857768;
        }

        public override string ToString()
        {
            return $"GoalThreeBlocks{{x={X},y={Y},z={Z}}}";
        }
    }

    public List<BetterBlockPos> DroppedItemsScan()
    {
        if (!BaritoneSettings.Settings().MineScanDroppedItems.Value)
        {
            return new List<BetterBlockPos>();
        }
        List<BetterBlockPos> ret = new();
        // TODO: ClientLevel.EntitiesForRendering
        ret.AddRange(_anticipatedDrops.Keys);
        return ret;
    }

    public static List<BetterBlockPos> SearchWorld(CalculationContext ctx, BlockOptionalMetaLookup filter, int max, List<BetterBlockPos> alreadyKnown, List<BetterBlockPos> blacklist, List<BetterBlockPos> dropped)
    {
        List<BetterBlockPos> locs = new();
        List<string> untracked = new(); // Changed from List<Block> to List<string> since Blocks() returns IEnumerable<string>
        foreach (var blockName in filter.Blocks())
        {
            string block = blockName; // BlockOptionalMetaLookup.Blocks() returns IEnumerable<string>
            if (CachedChunk.BlocksToKeepTrackOf.Contains(block))
            {
                BetterBlockPos? pf = ctx.Baritone.GetPlayerContext().PlayerFeet();
                // TODO: GetLocationsOf
            }
            else
            {
                untracked.Add(block);
            }
        }

        locs = Prune(ctx, locs, filter, max, blacklist, dropped);

        if (!untracked.Any() || (BaritoneSettings.Settings().ExtendCacheOnThreshold.Value && locs.Count < max))
        {
            // TODO: WorldScanner.ScanChunkRadius
        }

        locs.AddRange(alreadyKnown);

        return Prune(ctx, locs, filter, max, blacklist, dropped);
    }

    private bool AddNearby()
    {
        List<BetterBlockPos> dropped = DroppedItemsScan();
        _knownOreLocations.AddRange(dropped);

        BetterBlockPos playerFeet = Ctx.PlayerFeet() ?? new BetterBlockPos(0, 0, 0);
        BlockStateInterface bsi = new(Ctx);

        BlockOptionalMetaLookup? filter = FilterFilter();
        if (filter == null)
        {
            return false;
        }

        int searchDist = 10;
        for (int x = playerFeet.X - searchDist; x <= playerFeet.X + searchDist; x++)
        {
            for (int y = playerFeet.Y - searchDist; y <= playerFeet.Y + searchDist; y++)
            {
                for (int z = playerFeet.Z - searchDist; z <= playerFeet.Z + searchDist; z++)
                {
                    if (filter.Has(bsi.Get0(x, y, z)))
                    {
                        BetterBlockPos pos = new(x, y, z);
                        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/MineProcess.java:401
                        // Check if block is reachable before adding
                        var rot = Utils.RotationUtils.Reachable(Ctx, pos);
                        if (rot != null)
                        {
                            _knownOreLocations.Add(pos);
                        }
                    }
                }
            }
        }
        _knownOreLocations = Prune(new CalculationContext(Baritone), _knownOreLocations, filter, BaritoneSettings.Settings().MineMaxOreLocationsCount.Value, _blacklist, dropped);
        return true;
    }

    private static List<BetterBlockPos> Prune(CalculationContext ctx, List<BetterBlockPos> locs2, BlockOptionalMetaLookup filter, int max, List<BetterBlockPos> blacklist, List<BetterBlockPos> dropped)
    {
        dropped.RemoveAll(drop =>
        {
            foreach (var pos in locs2)
            {
                if (pos.DistanceSq(drop) <= 9 && filter.Has(ctx.Get(pos.X, pos.Y, pos.Z)) && PlausibleToBreak(ctx, pos))
                {
                    return true;
                }
            }
            return false;
        });
        List<BetterBlockPos> locs = locs2
                .Distinct()
                .Where(pos => !ctx.Bsi.WorldContainsLoadedChunk(pos.X, pos.Z) || filter.Has(ctx.Get(pos.X, pos.Y, pos.Z)) || dropped.Contains(pos))
                .Where(pos => PlausibleToBreak(ctx, pos))
                .Where(pos =>
                {
                    if (BaritoneSettings.Settings().AllowOnlyExposedOres.Value)
                    {
                        return IsNextToAir(ctx, pos);
                    }
                    else
                    {
                        return true;
                    }
                })
                .Where(pos => pos.Y >= BaritoneSettings.Settings().MinYLevelWhileMining.Value + ctx.World.DimensionType.MinY)
                .Where(pos => pos.Y <= BaritoneSettings.Settings().MaxYLevelWhileMining.Value)
                .Where(pos => !blacklist.Contains(pos))
                .OrderBy(pos =>
                {
                    var player = ctx.Baritone.GetPlayerContext().Player() as Entity;
                    if (player == null) return double.MaxValue;
                    var blockPos = player.BlockPosition();
                    var dx = blockPos.X - pos.X;
                    var dy = blockPos.Y - pos.Y;
                    var dz = blockPos.Z - pos.Z;
                    return dx * dx + dy * dy + dz * dz;
                })
                .ToList();

        if (locs.Count > max)
        {
            return locs.GetRange(0, max);
        }
        return locs;
    }

    public static bool IsNextToAir(CalculationContext ctx, BetterBlockPos pos)
    {
        int radius = BaritoneSettings.Settings().AllowOnlyExposedOresDistance.Value;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz) <= radius
                            && MovementHelper.IsTransparent(ctx.GetBlock(pos.X + dx, pos.Y + dy, pos.Z + dz)))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public static bool PlausibleToBreak(CalculationContext ctx, BetterBlockPos pos)
    {
        BlockState state = ctx.Bsi.Get0(pos.X, pos.Y, pos.Z);
        if (MovementHelper.GetMiningDurationTicks(ctx, pos.X, pos.Y, pos.Z, state, true) >= ActionCosts.CostInf)
        {
            return false;
        }
        if (MovementHelper.AvoidBreaking(ctx.Bsi, pos.X, pos.Y, pos.Z, state))
        {
            return false;
        }

        var above = ctx.Bsi.Get0(pos.Above().X, pos.Above().Y, pos.Above().Z);
        var below = ctx.Bsi.Get0(pos.Below().X, pos.Below().Y, pos.Below().Z);
        return !(above.Name.Contains("bedrock", StringComparison.OrdinalIgnoreCase) && below.Name.Contains("bedrock", StringComparison.OrdinalIgnoreCase));
    }

    public void MineByName(int quantity, params string[] blocks)
    {
        Mine(quantity, new BlockOptionalMetaLookup(blocks));
    }

    public void MineByName(params string[] blocks)
    {
        Mine(0, new BlockOptionalMetaLookup(blocks));
    }

    // Custom GoalRunAway that always returns false for IsInGoal and negative infinity for Heuristic
    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/MineProcess.java
    private class GoalRunAwayOverride : GoalRunAway
    {
        public GoalRunAwayOverride(double distance, int? maintainY, params BetterBlockPos[] from)
            : base(distance, maintainY, from)
        {
        }

        public override bool IsInGoal(int x, int y, int z)
        {
            // Always return false to force pathfinding away
            return false;
        }

        public override double Heuristic(int x, int y, int z)
        {
            // Return negative infinity to discourage pathfinding to this goal
            return double.NegativeInfinity;
        }
    }

    public void Mine(int quantity, BlockOptionalMetaLookup? filter)
    {
        _filter = filter;
        if (FilterFilter() == null)
        {
            _filter = null;
        }
        _desiredQuantity = quantity;
        _knownOreLocations = new List<BetterBlockPos>();
        _blacklist = new List<BetterBlockPos>();
        _branchPoint = null;
        _branchPointRunaway = null;
        _anticipatedDrops = new Dictionary<BetterBlockPos, long>();
        if (filter != null)
        {
            Rescan(new List<BetterBlockPos>(), new CalculationContext(Baritone));
        }
    }

    public void Cancel()
    {
        _filter = null;
        _desiredQuantity = 0;
        _knownOreLocations.Clear();
        _blacklist.Clear();
        _branchPoint = null;
        _branchPointRunaway = null;
        _anticipatedDrops.Clear();
    }

    private BlockOptionalMetaLookup? FilterFilter()
    {
        if (_filter == null)
        {
            return null;
        }
        if (!BaritoneSettings.Settings().AllowBreak.Value)
        {
            var allowedBlocks = _filter.Blocks()
                .Where(e => BaritoneSettings.Settings().AllowBreakAnyway.Value.Contains(e))
                .ToArray();
            BlockOptionalMetaLookup f = new(allowedBlocks);
            if (!f.Blocks().Any())
            {
                LogDirect("Unable to mine when allowBreak is false and target block is not in allowBreakAnyway!");
                return null;
            }
            return f;
        }
        return _filter;
    }
}
