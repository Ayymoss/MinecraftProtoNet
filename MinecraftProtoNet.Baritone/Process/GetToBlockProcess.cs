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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/GetToBlockProcess.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Utils;
using BaritoneSettings = MinecraftProtoNet.Baritone.Core.Baritone;

namespace MinecraftProtoNet.Baritone.Process;

/// <summary>
/// Get to block process implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/GetToBlockProcess.java
/// </summary>
public class GetToBlockProcess : BaritoneProcessHelper, IGetToBlockProcess
{
    private string? _gettingToBlockName; // Block name to get to
    private BlockOptionalMetaLookup? _filter;
    private List<BetterBlockPos>? _knownLocations;
    private List<BetterBlockPos>? _blacklist;
    private BetterBlockPos? _start;
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future tick counting logic
    private int _tickCount = 0;
    private int _arrivalTickCount = 0;
#pragma warning restore CS0414
    private bool _isActive;

    public GetToBlockProcess(IBaritone baritone) : base(baritone)
    {
    }

    public void GetToBlock(string blockName)
    {
        OnLostControl();
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/GetToBlockProcess.java:48-57
        _gettingToBlockName = blockName;
        _filter = new BlockOptionalMetaLookup(blockName);
        _isActive = true;
        _start = Ctx.PlayerFeet();
        _blacklist = new List<BetterBlockPos>();
        _arrivalTickCount = 0;
        _knownLocations = new List<BetterBlockPos>();
        Rescan(new List<BetterBlockPos>(), new CalculationContext(Baritone, false));
        LogDirect($"Getting to block: {blockName}");
    }

    public override bool IsActive() => _isActive;

    public override PathingCommand OnTick(bool calcFailed, bool isSafeToCancel)
    {
        if (!_isActive)
        {
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }

        if (_knownLocations == null)
        {
            _knownLocations = new List<BetterBlockPos>();
            // TODO: Implement rescan when world scanning is available
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/GetToBlockProcess.java:72
        }

        if (_knownLocations.Count == 0)
        {
            if (BaritoneSettings.Settings().ExploreForBlocks.Value && !calcFailed)
            {
                // Create exploration goal
                if (_start != null)
                {
                    var explorationGoal = new GoalRunAway(1, _start)
                    {
                        // Override to always return false for isInGoal
                    };
                    return new PathingCommand(explorationGoal, PathingCommandType.ForceRevalidateGoalAndPath);
                }
            }
            LogDirect("No known locations, canceling GetToBlock");
            if (isSafeToCancel)
            {
                OnLostControl();
            }
            return new PathingCommand(null, PathingCommandType.CancelAndSetGoal);
        }

        var goals = _knownLocations.Select(CreateGoal).ToArray();
        var goal = new GoalComposite(goals);

        if (calcFailed)
        {
            if (BaritoneSettings.Settings().BlacklistClosestOnFailure.Value)
            {
                LogDirect("Unable to find path, blacklisting closest instances...");
                BlacklistClosest();
                return OnTick(false, isSafeToCancel);
            }
            else
            {
                LogDirect("Unable to find path, canceling GetToBlock");
                if (isSafeToCancel)
                {
                    OnLostControl();
                }
                return new PathingCommand(goal, PathingCommandType.CancelAndSetGoal);
            }
        }

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/GetToBlockProcess.java:119-130
        // Periodic rescan
        var feet = Ctx.PlayerFeet();
        var pathingBehavior = (Behaviors.PathingBehavior)Baritone.GetPathingBehavior();
        var pathStart = pathingBehavior.PathStart();
        
        if (feet != null && goal.IsInGoal(feet) && (pathStart == null || goal.IsInGoal(pathStart)) && isSafeToCancel)
        {
            // We're there
            // Check for right-click on arrival
            // TODO: Implement right-click logic when interaction system is available
            OnLostControl();
            return new PathingCommand(null, PathingCommandType.CancelAndSetGoal);
        }

        return new PathingCommand(goal, PathingCommandType.RevalidateGoalAndPath);
    }

    public bool BlacklistClosest()
    {
        if (_knownLocations == null || _knownLocations.Count == 0)
        {
            return false;
        }

        var feet = Ctx.PlayerFeet();
        if (feet == null)
        {
            return false;
        }

        var newBlacklist = new List<BetterBlockPos>();
        var closest = _knownLocations
            .OrderBy(pos => feet.DistanceSq(pos))
            .FirstOrDefault();

        if (closest != null)
        {
            newBlacklist.Add(closest);
            _knownLocations.Remove(closest);
            
            // Also blacklist adjacent blocks
            foreach (var known in _knownLocations.ToList())
            {
                if (AreAdjacent(known, closest))
                {
                    newBlacklist.Add(known);
                    _knownLocations.Remove(known);
                }
            }
        }

        if (_blacklist != null && newBlacklist.Count > 0)
        {
            _blacklist.AddRange(newBlacklist);
        }
        return newBlacklist.Count > 0;
    }

    public override void OnLostControl()
    {
        _isActive = false;
        _gettingToBlockName = null;
        _filter = null;
        _knownLocations = null;
        _start = null;
        _blacklist = null;
        Baritone.GetInputOverrideHandler().ClearAllKeys();
    }

    public override string DisplayName()
    {
        if (_knownLocations == null || _knownLocations.Count == 0)
        {
            return "Exploring randomly to find block, no known locations";
        }
        return $"Get To Block, {_knownLocations.Count} known locations";
    }

    private Goal CreateGoal(BetterBlockPos pos)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/GetToBlockProcess.java:197-210
        // Check block type to determine goal type
        // For now, use GoalGetToBlock as default
        // TODO: Implement block type checking to determine if GoalTwoBlocks or GoalBlock should be used
        return new GoalGetToBlock(pos);
    }

    private static bool AreAdjacent(BetterBlockPos posA, BetterBlockPos posB)
    {
        int diffX = Math.Abs(posA.X - posB.X);
        int diffY = Math.Abs(posA.Y - posB.Y);
        int diffZ = Math.Abs(posA.Z - posB.Z);
        return (diffX + diffY + diffZ) == 1;
    }

    private void Rescan(List<BetterBlockPos> known, CalculationContext context)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/GetToBlockProcess.java:196-200
        if (_filter == null)
        {
            return;
        }
        var positions = MineProcess.SearchWorld(context, _filter, 64, known, _blacklist ?? new List<BetterBlockPos>(), new List<BetterBlockPos>());
        positions.RemoveAll(pos => _blacklist?.Contains(pos) == true);
        _knownLocations = positions;
    }
}

