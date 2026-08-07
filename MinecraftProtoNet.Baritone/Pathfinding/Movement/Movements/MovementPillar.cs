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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementPillar.java
 */

using System.Linq;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.Models.World.Chunk;
using BaritoneInput = MinecraftProtoNet.Baritone.Api.Utils.Input.Input;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for building up one block (pillaring).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementPillar.java
/// </summary>
public class MovementPillar(IBaritone baritone, BetterBlockPos start, BetterBlockPos end)
    : Movement(baritone, start, end, [start.Above(2)], start)
{
    public override double CalculateCost(CalculationContext context)
    {
        return Cost(context, Src.X, Src.Y, Src.Z);
    }

    protected override HashSet<BetterBlockPos> CalculateValidPositions()
    {
        return [Src, Dest];
    }

    public static double Cost(CalculationContext context, int x, int y, int z)
    {
        var fromState = context.Get(x, y, z);
        // Reference: MovementPillar.java:66-68
        bool ladder = fromState.IsClimbable;
        var fromDown = context.Get(x, y - 1, z);

        if (!ladder)
        {
            // Reference: MovementPillar.java:71-73 - can't pillar from a ladder/vine onto something that isn't also climbable
            if (fromDown.IsClimbable)
            {
                return ActionCosts.CostInf;
            }
            // Reference: MovementPillar.java:74-76 - can't pillar up from a bottom slab onto a non ladder
            if (MovementHelper.IsBottomSlab(fromDown))
            {
                return ActionCosts.CostInf;
            }
        }

        // Reference: MovementPillar.java:78-82 - fence gate above (see issue #172)
        var toBreak = context.Get(x, y + 2, z);
        if (toBreak.IsFenceGate)
        {
            return ActionCosts.CostInf;
        }

        // Reference: MovementPillar.java:83-89 - allow ascending pillars of water, but only if we're already in one
        BlockState? srcUp = null;
        if (MovementHelper.IsWater(toBreak) && MovementHelper.IsWater(fromState))
        {
            srcUp = context.Get(x, y + 1, z);
            if (MovementHelper.IsWater(srcUp))
            {
                return ActionCosts.LadderUpOneCost;
            }
        }

        double placeCost = 0;
        if (!ladder)
        {
            // Reference: MovementPillar.java:91-99 - we need to place a block where we started to jump on it
            placeCost = context.CostOfPlacingAt(x, y, z, fromState);
            if (placeCost >= ActionCosts.CostInf)
            {
                return ActionCosts.CostInf;
            }
            // slightly (1/200th of a second) penalize pillaring on what's currently air
            if (fromDown.IsAir)
            {
                placeCost += 0.1;
            }
        }

        // Reference: MovementPillar.java:101-106 - standing in/on liquid cannot pillar
        if ((MovementHelper.IsLiquid(fromState) && !MovementHelper.CanPlaceAgainst(context.Bsi, x, y - 1, z))
            || (MovementHelper.IsLiquid(fromDown) && context.AssumeWalkOnWater))
        {
            return ActionCosts.CostInf;
        }

        // Reference: MovementPillar.java:107-110 - (from == LILY_PAD || from instanceof CarpetBlock) && !fluid empty
        if ((fromState.IsLilyPad || fromState.IsCarpet) && MovementHelper.IsLiquid(fromDown))
        {
            return ActionCosts.CostInf;
        }

        double hardness = MovementHelper.GetMiningDurationTicks(context, x, y + 2, z, toBreak, true);
        if (hardness >= ActionCosts.CostInf)
        {
            return ActionCosts.CostInf;
        }

        // Reference: MovementPillar.java:115-137 - ladder/vine above, falling block above our head
        if (hardness != 0)
        {
            if (toBreak.IsClimbable)
            {
                hardness = 0; // we won't actually need to break the ladder/vine because we're going to use it
            }
            else
            {
                var check = context.Get(x, y + 3, z); // the block on top of the one we break, could it fall on us?
                if (check.IsFallingBlock)
                {
                    // see MovementAscend's identical check for breaking a falling block above our head
                    srcUp ??= context.Get(x, y + 1, z);
                    if (!toBreak.IsFallingBlock || !srcUp.IsFallingBlock)
                    {
                        return ActionCosts.CostInf;
                    }
                }
            }
        }

        if (ladder)
        {
            return ActionCosts.LadderUpOneCost + hardness * 5;
        }
        else
        {
            return ActionCosts.JumpOneBlockCost + placeCost + context.JumpPenalty + hardness;
        }
    }

    public override MovementState UpdateState(MovementState state)
    {
        base.UpdateState(state);
        if (state.GetStatus() != MovementStatus.Running)
        {
            return state;
        }

        // Reference: MovementPillar.java:152-154 (playerFeet() is non-null during an active movement tick)
        var feet = Ctx.PlayerFeet()!;
        if (feet.Y < Src.Y)
        {
            return state.SetStatus(MovementStatus.Unreachable);
        }

        var player = (Ctx.Player() as Entity)!;

        var fromDown = BlockStateInterface.Get(Ctx, Src);
        // Reference: MovementPillar.java:157-168 - stay centered while swimming up a water column
        if (MovementHelper.IsWater(fromDown) && MovementHelper.IsWater(BlockStateInterface.Get(Ctx, Dest)))
        {
            state.SetTarget(new MovementState.MovementTarget(
                RotationUtils.CalcRotationFromVec3d(Ctx.PlayerHead()!, VecUtils.GetBlockPosCenter(Dest), Ctx.PlayerRotations()!),
                false));
            var swimCenter = VecUtils.GetBlockPosCenter(Dest);
            if (Math.Abs(player.Position.X - swimCenter.X) > 0.2 || Math.Abs(player.Position.Z - swimCenter.Z) > 0.2)
            {
                state.SetInput(BaritoneInput.MoveForward, true);
            }
            if (feet.Equals(Dest))
            {
                return state.SetStatus(MovementStatus.Success);
            }
            return state;
        }

        // Reference: MovementPillar.java:169
        bool ladder = fromDown.IsClimbable;

        // Reference: MovementPillar.java:171-176
        Rotation rotation = RotationUtils.CalcRotationFromVec3d(
            Ctx.PlayerHead()!,
            VecUtils.GetBlockPosCenter(PositionToPlace),
            Ctx.PlayerRotations()!);
        if (!ladder)
        {
            state.SetTarget(new MovementState.MovementTarget(Ctx.PlayerRotations()!.WithPitch(rotation.GetPitch()), true));
        }

        bool blockIsThere = MovementHelper.CanWalkOn(Ctx, Src) || ladder;
        if (ladder)
        {
            // Reference: MovementPillar.java:179-186 - climb straight up the column holding JUMP
            if (feet.Equals(Dest))
            {
                return state.SetStatus(MovementStatus.Success);
            }

            MovementHelper.MoveTowards(Ctx, state, Dest);
            state.SetInput(BaritoneInput.Jump, true);
            return state;
        }
        else
        {
            // Reference: MovementPillar.java:188-191 - get ready to place a throwaway block
            if (!((Core.Baritone)Baritone).GetInventoryBehavior().SelectThrowawayForLocation(true, Src.X, Src.Y, Src.Z))
            {
                return state.SetStatus(MovementStatus.Unreachable);
            }

            // Reference: MovementPillar.java:193-194 - we only right click once player.isSneaking, which happens
            // the tick after we request to sneak, so sneak unconditionally here.
            state.SetInput(BaritoneInput.Sneak, true);

            double diffX = player.Position.X - (Dest.X + 0.5);
            double diffZ = player.Position.Z - (Dest.Z + 0.5);
            double dist = Math.Sqrt(diffX * diffX + diffZ * diffZ);
            double flatMotion = Math.Sqrt(player.Velocity.X * player.Velocity.X + player.Velocity.Z * player.Velocity.Z);
            // Reference: MovementPillar.java:200-212 - 0.17 < 0.2 sneak limit
            if (dist > 0.17)
            {
                state.SetInput(BaritoneInput.MoveForward, true);
                state.SetTarget(new MovementState.MovementTarget(rotation, true));
            }
            else if (flatMotion < 0.05)
            {
                // If our Y coordinate is above our goal, stop jumping
                state.SetInput(BaritoneInput.Jump, player.Position.Y < Dest.Y);
            }

            // Reference: MovementPillar.java:215-229
            if (!blockIsThere)
            {
                var frState = BlockStateInterface.Get(Ctx, Src);
                var bsi = ((Core.Baritone)Baritone).Bsi!;
                if (!(frState.IsAir || MovementHelper.IsReplaceable(Src.X, Src.Y, Src.Z, frState, bsi)))
                {
                    var reach = RotationUtils.Reachable(Ctx, Src, Ctx.PlayerController().GetBlockReachDistance());
                    if (reach != null)
                    {
                        state.SetTarget(new MovementState.MovementTarget(reach, true));
                    }
                    state.SetInput(BaritoneInput.Jump, false); // breaking is like 5x slower when you're jumping
                    state.SetInput(BaritoneInput.ClickLeft, true);
                    blockIsThere = false;
                }
                else if (player.IsSneaking && (Ctx.IsLookingAt(Src.Below()) || Ctx.IsLookingAt(Src)) && player.Position.Y > Dest.Y + 0.1)
                {
                    state.SetInput(BaritoneInput.ClickRight, true);
                }
            }

            MovementDiag.Log($"PILLAR feet=({feet.X},{feet.Y},{feet.Z}) py={player.Position.Y:F2} dist={dist:F3} flatMot={flatMotion:F3} sneaking={player.IsSneaking} lookBelow={Ctx.IsLookingAt(Src.Below())} lookSrc={Ctx.IsLookingAt(Src)} yOK={player.Position.Y > Dest.Y + 0.1} blockThere={blockIsThere} inputs=[{string.Join(",", state.GetInputStates().Where(kv => kv.Value).Select(kv => kv.Key))}]");
        }

        // Reference: MovementPillar.java:232-235 - at our goal and the block below us is placed
        if (feet.Equals(Dest) && blockIsThere)
        {
            return state.SetStatus(MovementStatus.Success);
        }

        return state;
    }

    protected override bool Prepared(MovementState state)
    {
        // Reference: MovementPillar.java:242-247
        var feet = Ctx.PlayerFeet()!;
        if (feet.Equals(Src) || feet.Equals(Src.Below()))
        {
            var belowState = BlockStateInterface.Get(Ctx, Src.Below());
            if (belowState.IsClimbable)
            {
                state.SetInput(BaritoneInput.Sneak, true);
            }
        }
        // Reference: MovementPillar.java:248-250
        if (MovementHelper.IsWater(BlockStateInterface.Get(Ctx, Dest.Above())))
        {
            return true;
        }
        return base.Prepared(state);
    }
}

