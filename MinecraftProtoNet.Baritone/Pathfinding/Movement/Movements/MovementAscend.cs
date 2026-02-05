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
 * along with Baritone.  See <https://www.gnu.org/licenses/>.
 *
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementAscend.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Physics;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for ascending one block up.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementAscend.java
/// </summary>
public class MovementAscend(IBaritone baritone, BetterBlockPos src, BetterBlockPos dest)
    : Movement(baritone, src, dest, [dest, src.Above(2), dest.Above()], dest.Below())
{
    private int _ticksWithoutPlacement = 0;

    public override void Reset()
    {
        base.Reset();
        _ticksWithoutPlacement = 0;
    }

    public override double CalculateCost(CalculationContext context)
    {
        return Cost(context, Src.X, Src.Y, Src.Z, Dest.X, Dest.Z);
    }

    protected override HashSet<BetterBlockPos> CalculateValidPositions()
    {
        var dir = GetDirection();
        var horizontalDir = (dir.X, 0, dir.Z);
        var prior = new BetterBlockPos(Src.X - horizontalDir.X, Src.Y, Src.Z - horizontalDir.Z);
        return new HashSet<BetterBlockPos> { Src, Src.Above(), Dest, prior, prior.Above() };
    }

    public static double Cost(CalculationContext context, int x, int y, int z, int destX, int destZ)
    {
        var toPlace = context.Get(destX, y, destZ);
        double additionalPlacementCost = 0;
        if (!MovementHelper.CanWalkOn(context, destX, y, destZ, toPlace))
        {
            additionalPlacementCost = context.CostOfPlacingAt(destX, y, destZ, toPlace);
            if (additionalPlacementCost >= ActionCosts.CostInf) return ActionCosts.CostInf;
            if (!MovementHelper.IsReplaceable(destX, y, destZ, toPlace, context.Bsi)) return ActionCosts.CostInf;
            bool foundPlaceOption = false;
            for (int i = 0; i < 5; i++)
            {
                var dir = Movement.HorizontalsButAlsoDown[i];
                var normal = Direction.GetNormal(dir);
                int againstX = destX + normal.X;
                int againstY = y + normal.Y;
                int againstZ = destZ + normal.Z;
                if (againstX == x && againstZ == z) continue;
                if (MovementHelper.CanPlaceAgainst(context.Bsi, againstX, againstY, againstZ)) { foundPlaceOption = true; break; }
            }
            if (!foundPlaceOption) return ActionCosts.CostInf;
        }
        
        var srcUp2 = context.Get(x, y + 2, z);
        string srcUp2Name = srcUp2.Name;
        if (srcUp2Name.Contains("gravel", StringComparison.OrdinalIgnoreCase) || srcUp2Name.Contains("sand", StringComparison.OrdinalIgnoreCase)) { }
        
        var srcDown = context.Get(x, y - 1, z);
        bool jumpingFromBottomSlab = MovementHelper.IsBottomSlab(srcDown);
        bool jumpingToBottomSlab = MovementHelper.IsBottomSlab(toPlace);
        if (jumpingFromBottomSlab && !jumpingToBottomSlab) return ActionCosts.CostInf;
        
        double walk;
        if (jumpingToBottomSlab)
        {
            if (jumpingFromBottomSlab) { walk = Math.Max(ActionCosts.JumpOneBlockCost, ActionCosts.WalkOneBlockCost); walk += context.JumpPenalty; }
            else walk = ActionCosts.WalkOneBlockCost;
        }
        else
        {
            if (srcDown.Name.Contains("soul_sand", StringComparison.OrdinalIgnoreCase)) walk = Math.Max(ActionCosts.JumpOneBlockCost * 2.0, ActionCosts.WalkOneBlockCost * 2.0);
            else walk = Math.Max(ActionCosts.JumpOneBlockCost, ActionCosts.WalkOneBlockCost);
            walk += context.JumpPenalty;
        }

        double totalCost = walk + additionalPlacementCost;
        totalCost += MovementHelper.GetMiningDurationTicks(context, x, y + 2, z, srcUp2, false);
        if (totalCost >= ActionCosts.CostInf) return ActionCosts.CostInf;
        totalCost += MovementHelper.GetMiningDurationTicks(context, destX, y + 1, destZ, false);
        if (totalCost >= ActionCosts.CostInf) return ActionCosts.CostInf;
        totalCost += MovementHelper.GetMiningDurationTicks(context, destX, y + 2, destZ, true);
        return totalCost;
    }

    public override MovementState UpdateState(MovementState state)
    {
        var player = Ctx.Player() as Entity;
        if (player == null) return state;

        var feet = Ctx.PlayerFeet();
        if (feet != null && feet.Y < Src.Y)
        {
            Baritone.GetGameEventHandler().LogDirect($"Ascend: Player fell below Src level ({feet.Y} < {Src.Y}), unreachable");
            return state.SetStatus(MovementStatus.Unreachable);
        }
        base.UpdateState(state);
        
        if (state.GetStatus() != MovementStatus.Running) return state;

        if (feet != null && (feet.Equals(Dest) || feet.Equals(new BetterBlockPos(Dest.X + GetDirection().X, Dest.Y, Dest.Z + GetDirection().Z))))
        {
            Baritone.GetGameEventHandler().LogDirect($"Ascend: Success at {feet}");
            return state.SetStatus(MovementStatus.Success);
        }

        if (PositionToPlace != null)
        {
            var jumpingOnto = BlockStateInterface.Get(Ctx, PositionToPlace);
            if (!MovementHelper.CanWalkOn(Ctx, PositionToPlace, jumpingOnto))
            {
                _ticksWithoutPlacement++;
                var placeResult = MovementHelper.AttemptToPlaceABlock(state, Baritone, Dest.Below(), false, true);
                
                // CRITICAL: Priority is placement. Disable movement forward to avoid jittering across the placement origin.
                state.SetInput(Input.MoveForward, false);
                
                if (placeResult == MovementHelper.PlaceResult.ReadyToPlace)
                {
                    state.SetInput(Input.Sneak, true);
                    if (state.GetInput(Input.Sneak) || player.IsSneaking) state.SetInput(Input.ClickRight, true);
                }
                
                if (_ticksWithoutPlacement > 10)
                {
                    Baritone.GetGameEventHandler().LogDirect($"Ascend: Stalled placement ({_ticksWithoutPlacement} ticks), moving back");
                    state.SetInput(Input.MoveBack, true);
                }

                if (feet != null && (int)Math.Floor((double)feet.Y) == Dest.Y - 1) state.SetInput(Input.Jump, true);
                return state;
            }
        }

        double dX = Dest.X + 0.5 - player.Position.X;
        double dZ = Dest.Z + 0.5 - player.Position.Z;
        double ab = Math.Sqrt(dX * dX + dZ * dZ);

        if (feet == null || !feet.Equals(Dest) || ab > 0.25) MovementHelper.MoveTowards(Ctx, state, Dest);
        
        var jumpingOntoFinal = BlockStateInterface.Get(Ctx, PositionToPlace!);
        var srcDownState = BlockStateInterface.Get(Ctx, Src.Below());
        if (MovementHelper.IsBottomSlab(jumpingOntoFinal) && !MovementHelper.IsBottomSlab(srcDownState)) return state;

        if (Core.Baritone.Settings().AssumeStep.Value || (feet != null && feet.Equals(Src.Above()))) return state;

        var dir = GetDirection();
        int xAxis = Math.Abs(dir.X);
        int zAxis = Math.Abs(dir.Z);
        double flatDistToNext = xAxis * Math.Abs((Dest.X + 0.5) - player.Position.X) + zAxis * Math.Abs((Dest.Z + 0.5) - player.Position.Z);
        double sideDist = zAxis * Math.Abs((Dest.X + 0.5) - player.Position.X) + xAxis * Math.Abs((Dest.Z + 0.5) - player.Position.Z);

        double lateralMotion = xAxis * player.Velocity.Z + zAxis * player.Velocity.X;
        if (Math.Abs(lateralMotion) > 0.1) return state;

        if (HeadBonkClear()) return state.SetInput(Input.Jump, true);
        if (flatDistToNext > 1.2 || sideDist > 0.2) return state;
        return state.SetInput(Input.Jump, true);
    }

    public bool HeadBonkClear()
    {
        if (!MovementHelper.CanWalkThrough(Ctx, Src.Above(2))) return false;
        var startUp = Src.Above(2);
        for (int i = 0; i < 4; i++)
        {
            var check = new BetterBlockPos(startUp.X + (i == 0 ? 1 : i == 1 ? -1 : 0), startUp.Y, startUp.Z + (i == 2 ? 1 : i == 3 ? -1 : 0));
            if (!MovementHelper.CanWalkThrough(Ctx, check)) return false;
        }
        return true;
    }

    protected override bool SafeToCancel(MovementState state) => state.GetStatus() != MovementStatus.Running || _ticksWithoutPlacement == 0;
}
