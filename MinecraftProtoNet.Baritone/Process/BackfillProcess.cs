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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BackfillProcess.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Models.World.Chunk;
using BaritoneInput = MinecraftProtoNet.Baritone.Api.Utils.Input.Input;
using JsonBlockState = MinecraftProtoNet.Core.Models.Json.BlockState;

namespace MinecraftProtoNet.Baritone.Process;

/// <summary>
/// Backfill process implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BackfillProcess.java
/// </summary>
public class BackfillProcess : BaritoneProcessHelper
{
    public Dictionary<BetterBlockPos, BlockState> BlocksToReplace = new();

    public BackfillProcess(IBaritone baritone) : base(baritone)
    {
    }

    public override bool IsActive()
    {
        if (Ctx.Player() == null || Ctx.World() == null)
        {
            return false;
        }
        if (!MinecraftProtoNet.Baritone.Core.Baritone.Settings().Backfill.Value)
        {
            return false;
        }
        if (MinecraftProtoNet.Baritone.Core.Baritone.Settings().AllowParkour.Value)
        {
            LogDirect("Backfill cannot be used with allowParkour true");
            MinecraftProtoNet.Baritone.Core.Baritone.Settings().Backfill.Value = false;
            return false;
        }

        // Remove blocks that are no longer air or are in unloaded chunks
        foreach (var pos in BlocksToReplace.Keys.ToList())
        {
            var state = BlockStateInterface.Get(Ctx, pos);
            if (!state.Name.Contains("air", StringComparison.OrdinalIgnoreCase))
            {
                BlocksToReplace.Remove(pos);
            }
        }

        AmIBreakingABlockHMMMMMMM();
        Baritone.GetInputOverrideHandler().ClearAllKeys();

        return ToFillIn().Count > 0;
    }

    public override PathingCommand OnTick(bool calcFailed, bool isSafeToCancel)
    {
        if (!isSafeToCancel)
        {
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }

        Baritone.GetInputOverrideHandler().ClearAllKeys();
        foreach (var toPlace in ToFillIn())
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BackfillProcess.java:85-98
            var fake = new MovementState();
            var result = MovementHelper.AttemptToPlaceABlock(fake, Baritone, toPlace, false, false);
            switch (result)
            {
                case MovementHelper.PlaceResult.NoOption:
                    continue;
                case MovementHelper.PlaceResult.ReadyToPlace:
                    Baritone.GetInputOverrideHandler().SetInputForceState(BaritoneInput.ClickRight, true);
                    return new PathingCommand(null, PathingCommandType.RequestPause);
                case MovementHelper.PlaceResult.Attempting:
                    var rotation = fake.GetTarget()?.GetRotation() ?? Ctx.PlayerRotations() ?? new Rotation(0, 0);
                    Baritone.GetLookBehavior().UpdateTarget(rotation, true);
                    return new PathingCommand(null, PathingCommandType.RequestPause);
            }
        }

        return new PathingCommand(null, PathingCommandType.Defer);
    }

    private void AmIBreakingABlockHMMMMMMM()
    {
        var selected = Ctx.GetSelectedBlock();
        if (selected == null || !Baritone.GetPathingBehavior().IsPathing())
        {
            return;
        }
        var state = BlockStateInterface.Get(Ctx, selected);
        BlocksToReplace[selected] = state;
    }

    public List<BetterBlockPos> ToFillIn()
    {
        var feet = Ctx.PlayerFeet();
        if (feet == null)
        {
            return new List<BetterBlockPos>();
        }

        return BlocksToReplace
            .Keys
            .Where(pos => BlockStateInterface.Get(Ctx, pos).Name.Contains("air", StringComparison.OrdinalIgnoreCase))
            .Where(pos =>
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BackfillProcess.java:128-129
                // Check if placement is plausible using BuilderProcess
                var builderProcess = Baritone.GetBuilderProcess();
                if (builderProcess != null)
                {
                    // Create a default dirt block state for backfill (Json.BlockState for interface)
                    var dirtState = new JsonBlockState 
                    { 
                        Id = 0, 
                        Properties = new Dictionary<string, string>() 
                    };
                    return builderProcess.PlacementPlausible(pos, dirtState);
                }
                return true; // If builder process not available, assume plausible
            })
            .Where(pos => !PartOfCurrentMovement(pos))
            .OrderByDescending(pos => feet.DistanceSq(pos))
            .ToList();
    }

    private bool PartOfCurrentMovement(BetterBlockPos pos)
    {
        var exec = Baritone.GetPathingBehavior().GetCurrent();
        if (exec == null || exec.Finished() || exec.Failed())
        {
            return false;
        }
        var path = exec.GetPath();
        if (path == null)
        {
            return false;
        }
        var movements = path.Movements();
        if (exec.GetPosition() >= movements.Count)
        {
            return false;
        }
        var movement = movements[exec.GetPosition()];
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BackfillProcess.java:155-156
        // Check if pos is in movement.toBreakAll()
        // Use ToBreakAll() method which is public
        if (movement is Movement concreteMovement)
        {
            var toBreak = concreteMovement.ToBreakAll();
            return toBreak != null && Array.IndexOf(toBreak, pos) >= 0;
        }
        return false;
    }

    public override void OnLostControl()
    {
        if (BlocksToReplace != null && BlocksToReplace.Count > 0)
        {
            BlocksToReplace.Clear();
        }
    }

    public override string DisplayName() => "Backfill";

    public override bool IsTemporary() => true;

    public override double Priority() => 5;
}

