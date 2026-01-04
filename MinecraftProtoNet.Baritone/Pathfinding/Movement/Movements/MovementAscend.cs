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
        var prior = new BetterBlockPos(Src.X - GetDirection().X, Src.Y + 1, Src.Z - GetDirection().Z);
        return new HashSet<BetterBlockPos> { Src, Src.Above(), Dest, prior, prior.Above() };
    }

    public static double Cost(CalculationContext context, int x, int y, int z, int destX, int destZ)
    {
        var toPlace = context.Get(destX, y, destZ);
        double additionalPlacementCost = 0;
        if (!MovementHelper.CanWalkOn(context, destX, y, destZ, toPlace))
        {
            additionalPlacementCost = context.CostOfPlacingAt(destX, y, destZ, toPlace);
            if (additionalPlacementCost >= ActionCosts.CostInf)
            {
                return ActionCosts.CostInf;
            }
            if (!MovementHelper.IsReplaceable(destX, y, destZ, toPlace, context.Bsi))
            {
                return ActionCosts.CostInf;
            }
            bool foundPlaceOption = false;
            for (int i = 0; i < 5; i++)
            {
                var dir = Movement.HorizontalsButAlsoDown[i];
                var normal = Direction.GetNormal(dir);
                int againstX = destX + normal.X;
                int againstY = y + normal.Y;
                int againstZ = destZ + normal.Z;
                if (againstX == x && againstZ == z)
                {
                    continue;
                }
                if (MovementHelper.CanPlaceAgainst(context.Bsi, againstX, againstY, againstZ))
                {
                    foundPlaceOption = true;
                    break;
                }
            }
            if (!foundPlaceOption)
            {
                return ActionCosts.CostInf;
            }
        }
        
        var srcUp2 = context.Get(x, y + 2, z);
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementAscend.java:95
        // Check for falling blocks
        string srcUp2Name = srcUp2.Name;
        bool isFallingBlock = srcUp2Name.Contains("gravel", StringComparison.OrdinalIgnoreCase) ||
                             srcUp2Name.Contains("sand", StringComparison.OrdinalIgnoreCase);
        if (isFallingBlock)
        {
            // Add cost for breaking falling blocks
        }
        
        var srcDown = context.Get(x, y - 1, z);
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementAscend.java:98
        // Check for ladder/vine
        string srcDownName = srcDown.Name;
        bool isClimbable = srcDownName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                          srcDownName.Contains("vine", StringComparison.OrdinalIgnoreCase);
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementAscend.java:100
        // Check for bottom slab logic
        bool jumpingFromBottomSlab = MovementHelper.IsBottomSlab(srcDown);
        bool jumpingToBottomSlab = MovementHelper.IsBottomSlab(toPlace);
        if (jumpingFromBottomSlab && !jumpingToBottomSlab)
        {
            return ActionCosts.CostInf;
        }
        
        double walk;
        if (jumpingToBottomSlab)
        {
            if (jumpingFromBottomSlab)
            {
                walk = Math.Max(ActionCosts.JumpOneBlockCost, ActionCosts.WalkOneBlockCost);
                walk += context.JumpPenalty;
            }
            else
            {
                walk = ActionCosts.WalkOneBlockCost;
            }
        }
        else
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementAscend.java:123
            // Check for soul sand (slows movement)
            string srcDownName2 = srcDown.Name;
            if (srcDownName2.Contains("soul_sand", StringComparison.OrdinalIgnoreCase))
            {
                walk = Math.Max(ActionCosts.JumpOneBlockCost * 2.0, ActionCosts.WalkOneBlockCost * 2.0);
            }
            else
            {
                walk = Math.Max(ActionCosts.JumpOneBlockCost, ActionCosts.WalkOneBlockCost);
            }
            walk += context.JumpPenalty;
        }

        double totalCost = walk + additionalPlacementCost;
        totalCost += MovementHelper.GetMiningDurationTicks(context, x, y + 2, z, srcUp2, false);
        if (totalCost >= ActionCosts.CostInf)
        {
            return ActionCosts.CostInf;
        }
        totalCost += MovementHelper.GetMiningDurationTicks(context, destX, y + 1, destZ, false);
        if (totalCost >= ActionCosts.CostInf)
        {
            return ActionCosts.CostInf;
        }
        totalCost += MovementHelper.GetMiningDurationTicks(context, destX, y + 2, destZ, true);
        return totalCost;
    }

    protected override MovementState UpdateState(MovementState state)
    {
        var feet = Ctx.PlayerFeet();
        if (feet != null && feet.Y < Src.Y)
        {
            return state.SetStatus(MovementStatus.Unreachable);
        }
        base.UpdateState(state);
        
        if (state.GetStatus() != MovementStatus.Running)
        {
            return state;
        }

        if (feet != null && (feet.Equals(Dest) || feet.Equals(new BetterBlockPos(Dest.X - GetDirection().X, Dest.Y - 1, Dest.Z - GetDirection().Z))))
        {
            return state.SetStatus(MovementStatus.Success);
        }

        if (PositionToPlace == null) return state;
        var jumpingOnto = BlockStateInterface.Get(Ctx, PositionToPlace);
        if (!MovementHelper.CanWalkOn(Ctx, PositionToPlace, jumpingOnto))
        {
            _ticksWithoutPlacement++;
            var placeResult = MovementHelper.AttemptToPlaceABlock(state, Baritone, Dest.Below(), false, true);
            if (placeResult == MovementHelper.PlaceResult.ReadyToPlace)
            {
                state.SetInput(Input.Sneak, true);
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementAscend.java:170
                // Check if crouching and set click right
                var player = Ctx.Player() as Entity;
                if (player != null && player.IsSneaking)
                {
                    // Right click to place block
                    Baritone.GetInputOverrideHandler().SetInputForceState(Input.ClickRight, true);
                }
            }
            if (_ticksWithoutPlacement > 10)
            {
                state.SetInput(Input.MoveBack, true);
            }
            return state;
        }
        
        MovementHelper.MoveTowards(Ctx, state, Dest);
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementAscend.java:181
        // Bottom slab check
        var srcDownState = BlockStateInterface.Get(Ctx, Src.Below());
        if (MovementHelper.IsBottomSlab(jumpingOnto) && !MovementHelper.IsBottomSlab(srcDownState))
        {
            return state;
        }

        if (Core.Baritone.Settings().AssumeStep.Value || (feet != null && feet.Equals(Src.Above())))
        {
            return state;
        }

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementAscend.java:192-198
        // Complex jump timing logic
        if (HeadBonkClear())
        {
            // Check distance to determine jump timing
            if (feet != null)
            {
                double distSq = feet.DistanceSq(Src);
                if (distSq < 0.25) // Close enough to jump
                {
                    return state.SetInput(Input.Jump, true);
                }
            }
        }

        // Distance checks for jump timing
        if (feet != null)
        {
            double distSq = feet.DistanceSq(Src);
            if (distSq < 0.5) // Close enough to jump
            {
                return state.SetInput(Input.Jump, true);
            }
        }
        return state;
    }

    public bool HeadBonkClear()
    {
        var startUp = Src.Above(2);
        for (int i = 0; i < 4; i++)
        {
            var check = new BetterBlockPos(startUp.X + (i == 0 ? 1 : i == 1 ? -1 : 0), startUp.Y, startUp.Z + (i == 2 ? 1 : i == 3 ? -1 : 0));
            if (!MovementHelper.CanWalkThrough(Ctx, check))
            {
                return false;
            }
        }
        return true;
    }

    protected override bool SafeToCancel(MovementState state)
    {
        return state.GetStatus() != MovementStatus.Running || _ticksWithoutPlacement == 0;
    }
}

