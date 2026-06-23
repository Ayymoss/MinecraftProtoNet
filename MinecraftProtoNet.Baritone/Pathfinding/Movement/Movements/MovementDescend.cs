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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDescend.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Baritone.Utils.Pathing;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for descending one block down.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDescend.java
/// </summary>
public class MovementDescend(IBaritone baritone, BetterBlockPos start, BetterBlockPos end)
    : Movement(baritone, start, end, [end.Above(2), end.Above(), end], end.Below())
{
    private int _numTicks = 0;
    private bool _forceSafeMode = false;

    public override void Reset()
    {
        base.Reset();
        _numTicks = 0;
        _forceSafeMode = false;
    }

    /// <summary>
    /// Called by PathExecutor if needing safeMode can only be detected with knowledge about the next movement.
    /// </summary>
    public void ForceSafeMode()
    {
        _forceSafeMode = true;
    }

    public override double CalculateCost(CalculationContext context)
    {
        var result = new MutableMoveResult();
        Cost(context, Src.X, Src.Y, Src.Z, Dest.X, Dest.Z, result);
        if (result.Y != Dest.Y)
        {
            return ActionCosts.CostInf; // doesn't apply to us, this position is a fall not a descend
        }

        return result.Cost;
    }

    protected override HashSet<BetterBlockPos> CalculateValidPositions()
    {
        return new HashSet<BetterBlockPos> { Src, Dest.Above(), Dest };
    }

    public static void Cost(CalculationContext context, int x, int y, int z, int destX, int destZ, MutableMoveResult res)
    {
        double totalCost = 0;
        var destDown = context.Get(destX, y - 1, destZ);
        totalCost += MovementHelper.GetMiningDurationTicks(context, destX, y - 1, destZ, destDown, false);
        if (totalCost >= ActionCosts.CostInf)
        {
            return;
        }

        totalCost += MovementHelper.GetMiningDurationTicks(context, destX, y, destZ, false);
        if (totalCost >= ActionCosts.CostInf)
        {
            return;
        }

        totalCost += MovementHelper.GetMiningDurationTicks(context, destX, y + 1, destZ, true);
        if (totalCost >= ActionCosts.CostInf)
        {
            return;
        }

        var fromDown = context.Get(x, y - 1, z);
        // Reference: MovementDescend.java:96-99 - can't descend from a climbable (ladder/vine)
        if (MovementHelper.IsClimbable(fromDown))
        {
            return;
        }

        var below = context.Get(destX, y - 2, destZ);
        if (!MovementHelper.CanWalkOn(context, destX, y - 2, destZ, below))
        {
            DynamicFallCost(context, x, y, z, destX, destZ, totalCost, below, res);
            return;
        }

        // Reference: MovementDescend.java:117-119 - can't descend onto a ladder/vine
        if (destDown.Name.Equals("minecraft:ladder", StringComparison.OrdinalIgnoreCase) ||
            destDown.Name.Equals("minecraft:vine", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        // Reference: MovementDescend.java:120-122 - the water will freeze when we try to walk into it
        if (MovementHelper.CanUseFrostWalker(context, destDown))
        {
            return;
        }

        // we walk half the block plus 0.3 to get to the edge, then we walk the other 0.2 while falling.
        double walk = ActionCosts.WalkOffBlockCost;
        // Reference: MovementDescend.java:126-129 - soul sand under src applies its speed penalty to the 0.8 walk-off
        if (fromDown.IsSoulSand)
        {
            walk *= ActionCosts.WalkOneOverSoulSandCost / ActionCosts.WalkOneBlockCost;
        }
        totalCost += walk + Math.Max(ActionCosts.FallNBlocksCost[1], ActionCosts.CenterAfterFallCost);
        res.X = destX;
        res.Y = y - 1;
        res.Z = destZ;
        res.Cost = totalCost;
    }

    public static bool DynamicFallCost(CalculationContext context, int x, int y, int z, int destX, int destZ, double frontBreak,
        BlockState below, MutableMoveResult res)
    {
        // Reference: MovementDescend.java:138-143 - if we're breaking blocks in front (frontBreak != 0),
        // don't let a falling block fall through this column (it could replace the water we'd fall into)
        if (frontBreak != 0 && context.Get(destX, y + 2, destZ).IsFallingBlock)
        {
            return false;
        }

        if (!MovementHelper.CanWalkThrough(context, destX, y - 2, destZ, below))
        {
            return false;
        }

        double costSoFar = 0;
        int effectiveStartHeight = y;
        for (int fallHeight = 3; true; fallHeight++)
        {
            int newY = y - fallHeight;
            if (newY < context.World.DimensionType.MinY)
            {
                return false;
            }

            bool reachedMinimum = fallHeight >= context.MinFallHeight;
            var ontoBlock = context.Get(destX, newY, destZ);
            int unprotectedFallHeight = fallHeight - (y - effectiveStartHeight);
            double tentativeCost = ActionCosts.WalkOffBlockCost +
                                   ActionCosts.FallNBlocksCost[unprotectedFallHeight] +
                                   frontBreak + costSoFar;

            if (reachedMinimum && MovementHelper.IsWater(ontoBlock))
            {
                if (!MovementHelper.CanWalkThrough(context, destX, newY, destZ, ontoBlock))
                {
                    return false;
                }

                if (context.AssumeWalkOnWater)
                {
                    return false;
                }

                if (MovementHelper.IsFlowing(destX, newY, destZ, ontoBlock, context.Bsi))
                {
                    return false;
                }

                if (!MovementHelper.CanWalkOn(context, destX, newY - 1, destZ))
                {
                    return false;
                }

                res.X = destX;
                res.Y = newY;
                res.Z = destZ;
                res.Cost = tentativeCost;
                return false;
            }

            if (reachedMinimum && context.AllowFallIntoLava && MovementHelper.IsLava(ontoBlock))
            {
                res.X = destX;
                res.Y = newY;
                res.Z = destZ;
                res.Cost = tentativeCost;
                return false;
            }

            // Reference: MovementDescend.java:189-196 - grabbing a vine/ladder resets falling speed
            // (only below fall height 11 — past that we don't actually grab on)
            if (unprotectedFallHeight <= 11 && MovementHelper.IsClimbable(ontoBlock))
            {
                costSoFar += ActionCosts.FallNBlocksCost[unprotectedFallHeight - 1]; // we fall until the top of this block
                costSoFar += ActionCosts.LadderDownOneCost;
                effectiveStartHeight = newY;
                continue;
            }
            // Reference: MovementDescend.java:197-199
            if (MovementHelper.CanWalkThrough(context, destX, newY, destZ, ontoBlock))
            {
                continue;
            }

            if (!MovementHelper.CanWalkOn(context, destX, newY, destZ, ontoBlock))
            {
                return false;
            }

            if (MovementHelper.IsBottomSlab(ontoBlock))
            {
                return false;
            }

            if (reachedMinimum && unprotectedFallHeight <= context.MaxFallHeightNoWater + 1)
            {
                res.X = destX;
                res.Y = newY + 1;
                res.Z = destZ;
                res.Cost = tentativeCost;
                return false;
            }

            if (reachedMinimum && context.HasWaterBucket && unprotectedFallHeight <= context.MaxFallHeightBucket + 1)
            {
                res.X = destX;
                res.Y = newY + 1;
                res.Z = destZ;
                res.Cost = tentativeCost + context.PlaceBucketCost();
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public override MovementState UpdateState(MovementState state)
    {
        base.UpdateState(state);
        if (state.GetStatus() != MovementStatus.Running)
        {
            return state;
        }

        var playerFeet = Ctx.PlayerFeet();
        var fakeDest = new BetterBlockPos(Dest.X * 2 - Src.X, Dest.Y, Dest.Z * 2 - Src.Z);
        if (playerFeet != null && (playerFeet.Equals(Dest) || playerFeet.Equals(fakeDest)) &&
            (MovementHelper.IsLiquid(Ctx, Dest) || ((Ctx.Player() as Entity)?.Position.Y ?? 0) - Dest.Y < 0.5))
        {
            return state.SetStatus(MovementStatus.Success);
        }

        if (SafeMode())
        {
            double destX = (Src.X + 0.5) * 0.17 + (Dest.X + 0.5) * 0.83;
            double destZ = (Src.Z + 0.5) * 0.17 + (Dest.Z + 0.5) * 0.83;
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDescend.java:249
            // Set target rotation and move forward
            var destPos = new BetterBlockPos((int)Math.Floor(destX), Dest.Y, (int)Math.Floor(destZ));
            MovementHelper.MoveTowards(Ctx, state, destPos);
            return state;
        }

        // Reference: MovementDescend.java:260 - sneak when standing on a magma block
        if (playerFeet != null)
        {
            state.SetInput(Input.Sneak, Core.Baritone.Settings().AllowWalkOnMagmaBlocks.Value
                && BlockStateInterface.Get(Ctx, playerFeet.Below()).IsMagmaBlock);
        }

        // Reference: MovementDescend.java:262-268
        var player = Ctx.Player() as Entity;
        if (player != null && playerFeet != null && !playerFeet.Equals(Dest))
        {
            double diffX = player.Position.X - (Dest.X + 0.5);
            double diffZ = player.Position.Z - (Dest.Z + 0.5);
            double ab = Math.Sqrt(diffX * diffX + diffZ * diffZ);
            double x = player.Position.X - (Src.X + 0.5);
            double z = player.Position.Z - (Src.Z + 0.5);
            double fromStart = Math.Sqrt(x * x + z * z);

            if (!playerFeet.Equals(Dest) || ab > 0.25)
            {
                if (_numTicks++ < 20 && fromStart < 1.25)
                {
                    MovementHelper.MoveTowards(Ctx, state, fakeDest);
                }
                else
                {
                    MovementHelper.MoveTowards(Ctx, state, Dest);
                }
            }
        }

        return state;
    }

    public bool SafeMode()
    {
        if (_forceSafeMode)
        {
            return true;
        }

        var into = new BetterBlockPos(Dest.X + (Dest.X - Src.X), Dest.Y, Dest.Z + (Dest.Z - Src.Z));
        if (SkipToAscend())
        {
            return true;
        }

        for (int y = 0; y <= 2; y++)
        {
            if (MovementHelper.AvoidWalkingInto(BlockStateInterface.Get(Ctx, into.Above(y))))
            {
                return true;
            }
        }

        return false;
    }

    public bool SkipToAscend()
    {
        var into = new BetterBlockPos(Dest.X + (Dest.X - Src.X), Dest.Y, Dest.Z + (Dest.Z - Src.Z));
        return !MovementHelper.CanWalkThrough(Ctx, into) &&
               MovementHelper.CanWalkThrough(Ctx, into.Above()) &&
               MovementHelper.CanWalkThrough(Ctx, into.Above(2));
    }
}
