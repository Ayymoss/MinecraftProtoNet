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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDownward.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for going straight down one block.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDownward.java
/// </summary>
public class MovementDownward(IBaritone baritone, BetterBlockPos start, BetterBlockPos end) : Movement(baritone, start, end, [end])
{
    private int _numTicks = 0;

    public override void Reset()
    {
        base.Reset();
        _numTicks = 0;
    }

    public override double CalculateCost(CalculationContext context)
    {
        return Cost(context, Src.X, Src.Y, Src.Z);
    }

    protected override HashSet<BetterBlockPos> CalculateValidPositions()
    {
        return new HashSet<BetterBlockPos> { Src, Dest };
    }

    public static double Cost(CalculationContext context, int x, int y, int z)
    {
        if (!context.AllowDownward)
        {
            return ActionCosts.CostInf;
        }
        if (!MovementHelper.CanWalkOn(context, x, y - 2, z))
        {
            return ActionCosts.CostInf;
        }
        var down = context.Get(x, y - 1, z);
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDownward.java:62
        // Check for ladder/vine
        string downName = down.Name;
        bool isClimbable = downName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                          downName.Contains("vine", StringComparison.OrdinalIgnoreCase);
        if (isClimbable)
        {
            // Can descend on climbable blocks
            return ActionCosts.LadderDownOneCost;
        }
        //     return ActionCosts.LadderDownOneCost;
        // }
        return ActionCosts.FallNBlocksCost[1] + MovementHelper.GetMiningDurationTicks(context, x, y - 1, z, down, false);
    }

    protected override MovementState UpdateState(MovementState state)
    {
        base.UpdateState(state);
        if (state.GetStatus() != MovementStatus.Running)
        {
            return state;
        }

        var feet = Ctx.PlayerFeet();
        if (feet != null && feet.Equals(Dest))
        {
            return state.SetStatus(MovementStatus.Success);
        }
        else if (!PlayerInValidPosition())
        {
            return state.SetStatus(MovementStatus.Unreachable);
        }
        
        var player = Ctx.Player() as Entity;
        if (player == null)
        {
            return state;
        }
        
        double diffX = player.Position.X - (Dest.X + 0.5);
        double diffZ = player.Position.Z - (Dest.Z + 0.5);
        double ab = Math.Sqrt(diffX * diffX + diffZ * diffZ);

        if (_numTicks++ < 10 && ab < 0.2)
        {
            return state;
        }
        MovementHelper.MoveTowards(Ctx, state, PositionsToBreak[0]);
        return state;
    }
}

