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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDiagonal.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Baritone.Utils.Pathing;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Physics;
using MinecraftProtoNet.Core.State;
using BaritoneInput = MinecraftProtoNet.Baritone.Api.Utils.Input.Input;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for moving diagonally between blocks.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDiagonal.java
/// </summary>
public class MovementDiagonal : Movement
{
    private static readonly double Sqrt2 = Math.Sqrt(2);

    private readonly BlockFace _face1;
    private readonly BlockFace _face2;

    public MovementDiagonal(IBaritone baritone, BetterBlockPos src, BlockFace face1, BlockFace face2, int yOffset)
        : base(baritone, src, GetDest(src, face1, face2, yOffset), GetToBreak(src, face1, face2, yOffset))
    {
        _face1 = face1;
        _face2 = face2;
    }

    private static BetterBlockPos GetDest(BetterBlockPos src, BlockFace face1, BlockFace face2, int yOffset)
    {
        var pos = GetRelative(src, face1);
        pos = GetRelative(pos, face2);
        return new BetterBlockPos(pos.X, pos.Y + yOffset, pos.Z);
    }

    // Reference: MovementDiagonal.java:56 - positionsToBreak = {dir1, dir1.up, dir2, dir2.up, end, end.up}
    private static BetterBlockPos[] GetToBreak(BetterBlockPos src, BlockFace face1, BlockFace face2, int yOffset)
    {
        var dest = GetDest(src, face1, face2, yOffset);
        var diag1 = GetRelative(src, face1);
        var diag2 = GetRelative(src, face2);
        return [diag1, diag1.Above(), diag2, diag2.Above(), dest, dest.Above()];
    }

    private static BetterBlockPos GetRelative(BetterBlockPos pos, BlockFace face)
    {
        return face switch
        {
            BlockFace.North => pos.North(),
            BlockFace.South => pos.South(),
            BlockFace.East => pos.East(),
            BlockFace.West => pos.West(),
            _ => pos
        };
    }

    protected override bool SafeToCancel(MovementState state)
    {
        var player = Ctx.Player() as Entity;
        if (player == null)
        {
            return true;
        }

        var feet = Ctx.PlayerFeet();
        if (feet == null)
        {
            return true;
        }

        double offset = 0.25;
        double x = player.Position.X;
        double y = player.Position.Y - 1;
        double z = player.Position.Z;

        // standard
        if (feet.Equals(Src))
        {
            return true;
        }

        // both corners are walkable
        if (MovementHelper.CanWalkOn(Ctx, new BetterBlockPos(Src.X, Src.Y - 1, Dest.Z)) &&
            MovementHelper.CanWalkOn(Ctx, new BetterBlockPos(Dest.X, Src.Y - 1, Src.Z)))
        {
            return true;
        }

        // we are in a likely unwalkable corner, check for a supporting block
        var corner1 = new BetterBlockPos(Src.X, Src.Y, Dest.Z);
        var corner2 = new BetterBlockPos(Dest.X, Src.Y, Src.Z);
        if (feet.Equals(corner1) || feet.Equals(corner2))
        {
            return MovementHelper.CanWalkOn(Ctx, new BetterBlockPos((int)Math.Floor(x + offset), (int)Math.Floor(y), (int)Math.Floor(z + offset))) ||
                   MovementHelper.CanWalkOn(Ctx, new BetterBlockPos((int)Math.Floor(x + offset), (int)Math.Floor(y), (int)Math.Floor(z - offset))) ||
                   MovementHelper.CanWalkOn(Ctx, new BetterBlockPos((int)Math.Floor(x - offset), (int)Math.Floor(y), (int)Math.Floor(z + offset))) ||
                   MovementHelper.CanWalkOn(Ctx, new BetterBlockPos((int)Math.Floor(x - offset), (int)Math.Floor(y), (int)Math.Floor(z - offset)));
        }
        return true;
    }

    public override double CalculateCost(CalculationContext context)
    {
        var result = new MutableMoveResult();
        Cost(context, Src.X, Src.Y, Src.Z, Dest.X, Dest.Z, result);
        if (result.Y != Dest.Y)
        {
            return ActionCosts.CostInf; // doesn't apply to us, this position is incorrect
        }
        return result.Cost;
    }

    protected override HashSet<BetterBlockPos> CalculateValidPositions()
    {
        var diagA = new BetterBlockPos(Src.X, Src.Y, Dest.Z);
        var diagB = new BetterBlockPos(Dest.X, Src.Y, Src.Z);
        if (Dest.Y < Src.Y)
        {
            return new HashSet<BetterBlockPos> { Src, Dest.Above(), diagA, diagB, Dest, diagA.Below(), diagB.Below() };
        }
        if (Dest.Y > Src.Y)
        {
            return new HashSet<BetterBlockPos> { Src, Src.Above(), diagA, diagB, Dest, diagA.Above(), diagB.Above() };
        }
        return new HashSet<BetterBlockPos> { Src, Dest, diagA, diagB };
    }

    public static void Cost(CalculationContext context, int x, int y, int z, int destX, int destZ, MutableMoveResult res)
    {
        if (!MovementHelper.CanWalkThrough(context, destX, y + 1, destZ))
        {
            return;
        }
        var destInto = context.Get(destX, y, destZ);
        BlockState fromDown;
        bool ascend = false;
        BlockState destWalkOn;
        bool descend = false;
        bool frostWalker = false;
        bool sneaking = false;
        if (!MovementHelper.CanWalkThrough(context, destX, y, destZ, destInto))
        {
            // Reference: MovementDiagonal.java:122-128 - diagonal ascend
            ascend = true;
            if (!context.AllowDiagonalAscend
                || !MovementHelper.CanWalkThrough(context, x, y + 2, z)
                || !MovementHelper.CanWalkOn(context, destX, y, destZ, destInto)
                || !MovementHelper.CanWalkThrough(context, destX, y + 2, destZ))
            {
                return;
            }
            destWalkOn = destInto;
            fromDown = context.Get(x, y - 1, z);
        }
        else
        {
            destWalkOn = context.Get(destX, y - 1, destZ);
            fromDown = context.Get(x, y - 1, z);
            bool standingOnABlock = MovementHelper.MustBeSolidToWalkOn(context, x, y - 1, z, fromDown);
            frostWalker = standingOnABlock && MovementHelper.CanUseFrostWalker(context, destWalkOn);
            if (!frostWalker && !MovementHelper.CanWalkOn(context, destX, y - 1, destZ, destWalkOn))
            {
                // Reference: MovementDiagonal.java:134-139 - diagonal descend
                descend = true;
                if (!context.AllowDiagonalDescend
                    || !MovementHelper.CanWalkOn(context, destX, y - 2, destZ)
                    || !MovementHelper.CanWalkThrough(context, destX, y - 1, destZ, destWalkOn))
                {
                    return;
                }
            }
            // do this after checking for descends because jesus can't prevent the water from freezing
            frostWalker &= !context.AssumeWalkOnWater;
        }
        double multiplier = ActionCosts.WalkOneBlockCost;
        // Reference: MovementDiagonal.java:143-153 - soul sand / magma / water each affect half of our walking
        if (destWalkOn.IsSoulSand)
        {
            multiplier += (ActionCosts.WalkOneOverSoulSandCost - ActionCosts.WalkOneBlockCost) / 2;
        }
        else if (context.AllowWalkOnMagmaBlocks && destWalkOn.IsMagmaBlock)
        {
            multiplier += (ActionCosts.SneakOneBlockCost - ActionCosts.WalkOneBlockCost) / 2;
            sneaking = true;
        }
        else if (frostWalker)
        {
            // frostwalker lets us walk on water without the penalty
        }
        else if (destWalkOn.Name.Equals("minecraft:water", StringComparison.OrdinalIgnoreCase))
        {
            multiplier += context.WalkOnWaterOnePenalty * Sqrt2;
        }
        // Reference: MovementDiagonal.java:154-163 - climbable below aborts; soul sand / magma add half penalty
        if (MovementHelper.IsClimbable(fromDown))
        {
            return;
        }
        if (fromDown.IsSoulSand)
        {
            multiplier += (ActionCosts.WalkOneOverSoulSandCost - ActionCosts.WalkOneBlockCost) / 2;
        }
        else if (context.AllowWalkOnMagmaBlocks && fromDown.IsMagmaBlock)
        {
            multiplier += (ActionCosts.SneakOneBlockCost - ActionCosts.WalkOneBlockCost) / 2;
            sneaking = true;
        }
        // Reference: MovementDiagonal.java:164-171 - can't cut across a corner that is magma/lava
        var cuttingOver1 = context.Get(x, y - 1, destZ);
        if ((!context.AllowWalkOnMagmaBlocks && cuttingOver1.IsMagmaBlock) || MovementHelper.IsLava(cuttingOver1))
        {
            return;
        }
        var cuttingOver2 = context.Get(destX, y - 1, z);
        // NOTE: faithful to Java:169 which re-checks cuttingOver1 for the magma condition (upstream quirk); lava check uses cuttingOver2
        if ((!context.AllowWalkOnMagmaBlocks && cuttingOver1.IsMagmaBlock) || MovementHelper.IsLava(cuttingOver2))
        {
            return;
        }
        bool water = false;
        var startState = context.Get(x, y, z);
        if (MovementHelper.IsWater(startState) || MovementHelper.IsWater(destInto))
        {
            if (ascend)
            {
                return;
            }
            // Ignore previous multiplier - we're floating on water, not touching the blocks below
            multiplier = context.WaterWalkSpeed;
            water = true;
        }
        var pb0 = context.Get(x, y, destZ);
        var pb2 = context.Get(destX, y, z);
        if (ascend)
        {
            // Reference: MovementDiagonal.java:187-208
            bool aTop = MovementHelper.CanWalkThrough(context, x, y + 2, destZ);
            bool aMid = MovementHelper.CanWalkThrough(context, x, y + 1, destZ);
            bool aLow = MovementHelper.CanWalkThrough(context, x, y, destZ, pb0);
            bool bTop = MovementHelper.CanWalkThrough(context, destX, y + 2, z);
            bool bMid = MovementHelper.CanWalkThrough(context, destX, y + 1, z);
            bool bLow = MovementHelper.CanWalkThrough(context, destX, y, z, pb2);
            if ((!(aTop && aMid && aLow) && !(bTop && bMid && bLow)) // no option
                || MovementHelper.AvoidWalkingInto(pb0) // bad
                || MovementHelper.AvoidWalkingInto(pb2) // bad
                || (aTop && aMid && MovementHelper.CanWalkOn(context, x, y, destZ, pb0)) // we could just ascend
                || (bTop && bMid && MovementHelper.CanWalkOn(context, destX, y, z, pb2)) // we could just ascend
                || (!aTop && aMid && aLow) // head bonk A
                || (!bTop && bMid && bLow)) // head bonk B
            {
                return;
            }
            res.Cost = multiplier * Sqrt2 + ActionCosts.JumpOneBlockCost;
            res.X = destX;
            res.Z = destZ;
            res.Y = y + 1;
            return;
        }
        // Reference: MovementDiagonal.java:209-250 - corner-cutting / edging-around checks
        double optionA = MovementHelper.GetMiningDurationTicks(context, x, y, destZ, pb0, false);
        double optionB = MovementHelper.GetMiningDurationTicks(context, destX, y, z, pb2, false);
        if (optionA != 0 && optionB != 0)
        {
            return;
        }
        var pb1 = context.Get(x, y + 1, destZ);
        optionA += MovementHelper.GetMiningDurationTicks(context, x, y + 1, destZ, pb1, true);
        if (optionA != 0 && optionB != 0)
        {
            return;
        }
        var pb3 = context.Get(destX, y + 1, z);
        if (optionA == 0 && ((MovementHelper.AvoidWalkingInto(pb2) && !pb2.Name.Equals("minecraft:water", StringComparison.OrdinalIgnoreCase)) || MovementHelper.AvoidWalkingInto(pb3)))
        {
            return;
        }
        optionB += MovementHelper.GetMiningDurationTicks(context, destX, y + 1, z, pb3, true);
        if (optionA != 0 && optionB != 0)
        {
            return;
        }
        if (optionB == 0 && ((MovementHelper.AvoidWalkingInto(pb0) && !pb0.Name.Equals("minecraft:water", StringComparison.OrdinalIgnoreCase)) || MovementHelper.AvoidWalkingInto(pb1)))
        {
            return;
        }
        if (optionA != 0 || optionB != 0)
        {
            multiplier *= Sqrt2 - 0.001; // TODO tune
            if (MovementHelper.IsClimbable(startState))
            {
                // edging around doesn't work if doing so would climb a ladder or vine instead of moving sideways
                return;
            }
        }
        else
        {
            // only can sprint if not edging around
            if (context.CanSprint && !water && !sneaking)
            {
                // soul sand is sprintable too, so we don't check for it
                multiplier *= ActionCosts.SprintMultiplier;
            }
        }
        res.Cost = multiplier * Sqrt2;
        if (descend)
        {
            res.Cost += Math.Max(ActionCosts.FallNBlocksCost[1], ActionCosts.CenterAfterFallCost);
            res.Y = y - 1;
        }
        else
        {
            res.Y = y;
        }
        res.X = destX;
        res.Z = destZ;
    }

    public override MovementState UpdateState(MovementState state)
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
        if (!PlayerInValidPosition()
            && !(MovementHelper.IsLiquid(Ctx, Src) && GetValidPositions().Contains((feet ?? Src).Above())))
        {
            return state.SetStatus(MovementStatus.Unreachable);
        }

        var player = Ctx.Player() as Entity;
        if (player != null && Dest.Y > Src.Y && player.Position.Y < Src.Y + 0.1 && player.HorizontalCollision)
        {
            state.SetInput(BaritoneInput.Jump, true);
        }

        if (Sprint())
        {
            state.SetInput(BaritoneInput.Sprint, true);
        }

        // Reference: MovementDiagonal.java:280 - sneak if walking on magma
        state.SetInput(BaritoneInput.Sneak, Core.Baritone.Settings().AllowWalkOnMagmaBlocks.Value
            && MovementHelper.SteppingOnBlocks(Ctx).Any(b => BlockStateInterface.Get(Ctx, b).IsMagmaBlock));

        MovementHelper.MoveTowards(Ctx, state, Dest);
        return state;
    }

    private bool Sprint()
    {
        var feet = Ctx.PlayerFeet();
        if (feet != null && MovementHelper.IsLiquid(Ctx, feet) && !Core.Baritone.Settings().SprintInWater.Value)
        {
            return false;
        }
        for (int i = 0; i < 4; i++)
        {
            if (!MovementHelper.CanWalkThrough(Ctx, PositionsToBreak[i]))
            {
                return false;
            }
        }
        return true;
    }

    // Reference: MovementDiagonal.java:302-315 - only the destination column (indices 4,5) is broken
    public override List<BetterBlockPos> ToBreak(BlockStateInterface bsi)
    {
        if (ToBreakCached != null)
        {
            return ToBreakCached;
        }
        var result = new List<BetterBlockPos>();
        for (int i = 4; i < 6; i++)
        {
            if (!MovementHelper.CanWalkThrough(bsi, PositionsToBreak[i].X, PositionsToBreak[i].Y, PositionsToBreak[i].Z))
            {
                result.Add(PositionsToBreak[i]);
            }
        }
        ToBreakCached = result;
        return result;
    }

    // Reference: MovementDiagonal.java:317-330 - the four corner-column blocks (indices 0..3) we might edge into
    public override List<BetterBlockPos> ToWalkInto(BlockStateInterface bsi)
    {
        var result = new List<BetterBlockPos>();
        for (int i = 0; i < 4; i++)
        {
            if (!MovementHelper.CanWalkThrough(bsi, PositionsToBreak[i].X, PositionsToBreak[i].Y, PositionsToBreak[i].Z))
            {
                result.Add(PositionsToBreak[i]);
            }
        }
        ToWalkIntoCached = result;
        return result;
    }

    protected override bool Prepared(MovementState state)
    {
        return true;
    }
}
