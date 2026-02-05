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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FarmProcess.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Utils;

namespace MinecraftProtoNet.Baritone.Process;

/// <summary>
/// Farm process implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FarmProcess.java
/// </summary>
public class FarmProcess : BaritoneProcessHelper, IFarmProcess
{
    private bool _active;
    private List<BetterBlockPos>? _locations;
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future tick counting logic
    private int _tickCount;
#pragma warning restore CS0414
    private int _range;
    private BetterBlockPos? _center;

    public FarmProcess(IBaritone baritone) : base(baritone)
    {
    }

    public override bool IsActive() => _active;

    public void Farm()
    {
        Farm(64, null);
    }

    public void Farm(int range)
    {
        Farm(range, null);
    }

    public void Farm(int range, BetterBlockPos? pos)
    {
        if (pos == null)
        {
            _center = Ctx.PlayerFeet();
        }
        else
        {
            _center = pos;
        }
        _range = range;
        _active = true;
        _locations = null;
        _tickCount = 0;
    }

    public override PathingCommand OnTick(bool calcFailed, bool isSafeToCancel)
    {
        if (!_active || _center == null)
        {
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }

        if (calcFailed)
        {
            LogDirect("Farm calculation failed");
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FarmProcess.java:86
        // Scan for farmable blocks
        if (_locations == null)
        {
            _locations = ScanForFarmableBlocks();
        }

        if (_locations == null || _locations.Count == 0)
        {
            LogDirect("No farmable blocks found");
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }

        // Create goals for farmable locations
        var goals = _locations.Select(pos => new GoalGetToBlock(pos) as Goal).ToArray();
        var goal = new GoalComposite(goals);

        return new PathingCommand(goal, PathingCommandType.RevalidateGoalAndPath);
    }

    private List<BetterBlockPos> ScanForFarmableBlocks()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FarmProcess.java:107-134
        // Scan world for farmable blocks (crops, etc.)
        var locations = new List<BetterBlockPos>();
        if (_center == null) return locations;
        
        var bsi = new BlockStateInterface(Ctx);
        int range = _range;
        int minX = _center.X - range;
        int maxX = _center.X + range;
        int minZ = _center.Z - range;
        int maxZ = _center.Z + range;
        
        // Farmable crop block names (from Minecraft crop tags)
        var farmableBlocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wheat", "carrot", "potato", "beetroot", "beetroots",
            "nether_wart", "pumpkin_stem", "melon_stem", "cocoa",
            "sweet_berry_bush", "sugar_cane", "bamboo", "cactus"
        };
        
        // Scan area for farmable blocks
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                // Check multiple Y levels (crops can be at different heights)
                for (int y = _center.Y - 10; y <= _center.Y + 10; y++)
                {
                    var state = bsi.Get0(x, y, z);
                    if (state != null)
                    {
                        string blockName = state.Name;
                        // Check if block is a farmable crop
                        if (farmableBlocks.Any(fb => blockName.Contains(fb, StringComparison.OrdinalIgnoreCase)))
                        {
                            locations.Add(new BetterBlockPos(x, y, z));
                        }
                    }
                }
            }
        }
        
        return locations;
    }

    public override void OnLostControl()
    {
        _active = false;
        _locations = null;
        _center = null;
        _tickCount = 0;
    }

    public override string DisplayName()
    {
        if (!_active)
        {
            return "Farm (inactive)";
        }
        return $"Farming (range: {_range})";
    }

    public void Cancel()
    {
        OnLostControl();
    }
}

