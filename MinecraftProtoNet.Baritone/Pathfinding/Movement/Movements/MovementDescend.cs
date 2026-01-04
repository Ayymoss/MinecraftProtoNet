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
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDescend.java:96
        // Check for ladder/vine
        string fromDownName = fromDown.Name;
        bool isClimbable = fromDownName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                          fromDownName.Contains("vine", StringComparison.OrdinalIgnoreCase);
        if (isClimbable)
        {
            // Can descend on climbable blocks
        }

        var below = context.Get(destX, y - 2, destZ);
        if (!MovementHelper.CanWalkOn(context, destX, y - 2, destZ, below))
        {
            DynamicFallCost(context, x, y, z, destX, destZ, totalCost, below, res);
            return;
        }

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDescend.java:105
        // Check for ladder/vine, frost walker
        string destDownName = destDown.Name;
        // Reuse isClimbable from above (declared at line 99)
        bool isClimbable2 = destDownName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                          destDownName.Contains("vine", StringComparison.OrdinalIgnoreCase);
        if (isClimbable2)
        {
            // Can descend on climbable blocks
        }
        if (MovementHelper.CanUseFrostWalker(context, destDown))
        {
            return;
        }

        double walk = ActionCosts.WalkOffBlockCost;
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDescend.java:112
        // Check for soul sand (slows movement)
        // Reuse destDownName from above
        if (destDownName.Contains("soul_sand", StringComparison.OrdinalIgnoreCase))
        {
            walk *= 2.0;
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
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDescend.java:123-126
        // Check for falling blocks
        string belowName = below.Name;
        bool isFallingBlock = belowName.Contains("gravel", StringComparison.OrdinalIgnoreCase) ||
                             belowName.Contains("sand", StringComparison.OrdinalIgnoreCase);
        if (frontBreak != 0 && isFallingBlock)
        {
            // Add cost for breaking falling blocks
            frontBreak += MovementHelper.GetMiningDurationTicks(context, destX, y - 1, destZ, below, true);
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
                                   ActionCosts.FallNBlocksCost[Math.Min(unprotectedFallHeight, ActionCosts.FallNBlocksCost.Length - 1)] +
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

            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDescend.java:189
            // Check for vine/ladder
            string ontoName = ontoBlock.Name;
            bool isClimbable = ontoName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                              ontoName.Contains("vine", StringComparison.OrdinalIgnoreCase);
            if (isClimbable || MovementHelper.CanWalkThrough(context, destX, newY, destZ, ontoBlock))
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

    protected override MovementState UpdateState(MovementState state)
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

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDescend.java:254
        // Complex movement logic
        if (playerFeet != null && !playerFeet.Equals(Dest))
        {
            if (_numTicks++ < 20)
            {
                MovementHelper.MoveTowards(Ctx, state, fakeDest);
            }
            else
            {
                MovementHelper.MoveTowards(Ctx, state, Dest);
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

        var into = new BetterBlockPos(Dest.X - (Dest.X - Src.X), Dest.Y, Dest.Z - (Dest.Z - Src.Z));
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
        var into = new BetterBlockPos(Dest.X - (Dest.X - Src.X), Dest.Y, Dest.Z - (Dest.Z - Src.Z));
        return !MovementHelper.CanWalkThrough(Ctx, into) &&
               MovementHelper.CanWalkThrough(Ctx, into.Above()) &&
               MovementHelper.CanWalkThrough(Ctx, into.Above(2));
    }
}
