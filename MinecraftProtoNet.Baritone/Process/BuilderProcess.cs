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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BuilderProcess.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Utils;
using JsonBlockState = MinecraftProtoNet.Core.Models.Json.BlockState;

namespace MinecraftProtoNet.Baritone.Process;

/// <summary>
/// Builder process implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BuilderProcess.java
/// </summary>
public class BuilderProcess(IBaritone baritone) : BaritoneProcessHelper(baritone), IBuilderProcess
{
    private HashSet<BetterBlockPos>? _incorrectPositions;
    private string? _name;
    private bool _paused;
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future layer/height logic
    private int _layer;
    private int _stopAtHeight;
#pragma warning restore CS0414

    public override bool IsActive()
    {
        return _name != null && !_paused;
    }

    public override PathingCommand OnTick(bool calcFailed, bool isSafeToCancel)
    {
        if (_name == null)
        {
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }

        if (calcFailed)
        {
            LogDirect("Build calculation failed");
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BuilderProcess.java:62-64
        // Calculate positions to build based on schematic
        // For now, return DEFER until schematic loading is implemented
        // TODO: When schematic system is available, calculate incorrect positions and create goals
        if (_incorrectPositions == null)
        {
            _incorrectPositions = new HashSet<BetterBlockPos>();
        }
        
        if (_incorrectPositions.Count == 0)
        {
            LogDirect("No blocks to build");
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }
        
        // Create goals for positions that need building
        var goals = _incorrectPositions.Select(pos => new GoalBlock(pos) as Goal).ToArray();
        var goal = new GoalComposite(goals);
        return new PathingCommand(goal, PathingCommandType.RevalidateGoalAndPath);
    }

    public override void OnLostControl()
    {
        _name = null;
        _incorrectPositions = null;
        _paused = false;
        _layer = 0;
        _stopAtHeight = 0;
    }

    public override string DisplayName()
    {
        if (_name == null)
        {
            return "Builder (inactive)";
        }
        return $"Building {_name}";
    }

    public void Build(string file)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BuilderProcess.java:87-100
        // Load schematic from file
        _name = file;
        _paused = false;
        _layer = 0;
        _incorrectPositions = new HashSet<BetterBlockPos>();
        
        try
        {
            // TODO: When schematic loading system is available, load schematic from file
            // In Java, this uses SchematicLoader.loadSchematic(file) which returns an ISchematic
            // For now, log that schematic loading is not yet implemented
            LogDirect($"Starting build: {file} (schematic loading not yet implemented)");
        }
        catch (Exception e)
        {
            LogDirect($"Failed to load schematic: {e.Message}");
            OnLostControl();
        }
    }

    public void BuildDirectly(string file)
    {
        Build(file);
    }

    public void Pause()
    {
        _paused = true;
    }

    public void Resume()
    {
        _paused = false;
    }

    public bool IsPaused() => _paused;

    public void ClearArea(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BuilderProcess.java:113-124
        // Clear area logic - mark blocks in area for breaking
        LogDirect($"Clearing area from ({x1}, {y1}, {z1}) to ({x2}, {y2}, {z2})");
        
        if (_incorrectPositions == null)
        {
            _incorrectPositions = new HashSet<BetterBlockPos>();
        }
        
        // Add all positions in the area to incorrect positions (to be cleared)
        int minX = Math.Min(x1, x2);
        int maxX = Math.Max(x1, x2);
        int minY = Math.Min(y1, y2);
        int maxY = Math.Max(y1, y2);
        int minZ = Math.Min(z1, z2);
        int maxZ = Math.Max(z1, z2);
        
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    _incorrectPositions.Add(new BetterBlockPos(x, y, z));
                }
            }
        }
    }

    public void Cancel()
    {
        OnLostControl();
    }

    JsonBlockState? IBuilderProcess.PlaceAt(int x, int y, int z, JsonBlockState current)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BuilderProcess.java:255-267
        // For now, return null as schematic support is not implemented
        // This method would return the desired block state from the schematic
        return null;
    }

    bool IBuilderProcess.PlacementPlausible(BetterBlockPos pos, JsonBlockState state)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BuilderProcess.java:124-126
        // Check if block placement is plausible
        // In Java, this checks if the block can be placed at the position
        // For now, do basic checks: ensure position is loaded and not solid where we're standing
        var bsi = new BlockStateInterface(Ctx);
        if (!bsi.IsLoaded(pos.X, pos.Z))
        {
            return false; // Can't place in unloaded chunks
        }
        
        // Check if there's a solid block below to place on
        var below = bsi.Get0(pos.X, pos.Y - 1, pos.Z);
        if (below == null || below.IsAir)
        {
            return false; // Can't place on air
        }
        
        // Check if the position itself is air or replaceable
        var current = bsi.Get0(pos.X, pos.Y, pos.Z);
        if (current != null && !current.IsAir && !MovementHelper.IsReplaceable(pos.X, pos.Y, pos.Z, current, bsi))
        {
            return false; // Position is not replaceable
        }
        
        return true;
    }
}

