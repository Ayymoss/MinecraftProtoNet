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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/InventoryPauserProcess.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Process;

/// <summary>
/// Inventory pauser process implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/InventoryPauserProcess.java
/// </summary>
public class InventoryPauserProcess : BaritoneProcessHelper
{
    private bool _pauseRequestedLastTick;
    private bool _safeToCancelLastTick;
    private int _ticksOfStationary;

    public InventoryPauserProcess(IBaritone baritone) : base(baritone)
    {
    }

    public override bool IsActive()
    {
        if (Ctx.Player() == null || Ctx.World() == null)
        {
            return false;
        }
        return true;
    }

    private double Motion()
    {
        var player = Ctx.Player() as Entity;
        if (player == null)
        {
            return 0;
        }
        var delta = player.Velocity;
        return Math.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
    }

    private bool StationaryNow()
    {
        return Motion() < 0.00001;
    }

    public bool StationaryForInventoryMove()
    {
        _pauseRequestedLastTick = true;
        return _safeToCancelLastTick && _ticksOfStationary > 1;
    }

    public override PathingCommand OnTick(bool calcFailed, bool isSafeToCancel)
    {
        _safeToCancelLastTick = isSafeToCancel;
        if (_pauseRequestedLastTick)
        {
            _pauseRequestedLastTick = false;
            if (StationaryNow())
            {
                _ticksOfStationary++;
            }
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }
        _ticksOfStationary = 0;
        return new PathingCommand(null, PathingCommandType.Defer);
    }

    public override void OnLostControl()
    {
        // Nothing to do
    }

    public override string DisplayName() => "inventory pauser";

    public override double Priority() => 5.1; // slightly higher than backfill

    public override bool IsTemporary() => true;
}

