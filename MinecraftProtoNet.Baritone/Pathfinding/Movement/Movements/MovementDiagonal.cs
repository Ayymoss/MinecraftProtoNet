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
using MinecraftProtoNet.Baritone.Utils.Pathing;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for diagonal movement (moving diagonally to an adjacent block).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDiagonal.java
/// </summary>
public class MovementDiagonal : Movement
{
    private static readonly double Sqrt2 = Math.Sqrt(2);

    public MovementDiagonal(IBaritone baritone, BetterBlockPos start, BlockFace dir1, BlockFace dir2, int dy)
        : this(baritone, start, GetRelative(start, dir1), GetRelative(start, dir2), dir2, dy)
    {
    }

    private MovementDiagonal(IBaritone baritone, BetterBlockPos start, BetterBlockPos dir1, BetterBlockPos dir2, BlockFace dir2Face, int dy)
        : this(baritone, start, GetRelative(dir1, dir2Face).Above(dy), dir1, dir2)
    {
    }

    private MovementDiagonal(IBaritone baritone, BetterBlockPos start, BetterBlockPos end, BetterBlockPos dir1, BetterBlockPos dir2)
        : base(baritone, start, end, new[] { dir1, dir1.Above(), dir2, dir2.Above(), end, end.Above() })
    {
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
        
        if (feet.Equals(Src))
        {
            return true;
        }
        
        // Both corners are walkable
        if (MovementHelper.CanWalkOn(Ctx, new BetterBlockPos(Src.X, Src.Y - 1, Dest.Z)) &&
            MovementHelper.CanWalkOn(Ctx, new BetterBlockPos(Dest.X, Src.Y - 1, Src.Z)))
        {
            return true;
        }
        
        // We are in a likely unwalkable corner, check for a supporting block
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
        
        if (!MovementHelper.CanWalkThrough(context, destX, y, destZ, destInto))
        {
            ascend = true;
            if (!context.AllowDiagonalAscend || 
                !MovementHelper.CanWalkThrough(context, x, y + 2, z) || 
                !MovementHelper.CanWalkOn(context, destX, y, destZ, destInto) || 
                !MovementHelper.CanWalkThrough(context, destX, y + 2, destZ))
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
                descend = true;
                if (!context.AllowDiagonalDescend || 
                    !MovementHelper.CanWalkOn(context, destX, y - 2, destZ) || 
                    !MovementHelper.CanWalkThrough(context, destX, y - 1, destZ, destWalkOn))
                {
                    return;
                }
            }
            frostWalker &= !context.AssumeWalkOnWater;
        }
        
        double multiplier = ActionCosts.WalkOneBlockCost;
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDiagonal.java:181
        // Check for soul sand, water, frost walker
        string destWalkOnName = destWalkOn.Name;
        bool onSoulSand = destWalkOnName.Contains("soul_sand", StringComparison.OrdinalIgnoreCase);
        if (onSoulSand)
        {
            multiplier *= 2.0; // Soul sand slows movement
        }
        if (MovementHelper.IsWater(destInto) || MovementHelper.IsWater(destWalkOn))
        {
            multiplier = context.WaterWalkSpeed;
        }
        if (frostWalker)
        {
            multiplier = ActionCosts.WalkOneBlockCost; // Frost walker allows normal walking on water
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDiagonal.java:183
        // Check for fromDown block type
        string fromDownName = fromDown.Name;
        bool fromOnSoulSand = fromDownName.Contains("soul_sand", StringComparison.OrdinalIgnoreCase);
        if (fromOnSoulSand)
        {
            multiplier *= 2.0;
        }
        
        double hardness1 = MovementHelper.GetMiningDurationTicks(context, destX, y, destZ, destInto, false);
        if (hardness1 >= ActionCosts.CostInf)
        {
            return;
        }
        double hardness2 = MovementHelper.GetMiningDurationTicks(context, destX, y + 1, destZ, false);
        if (hardness1 == 0 && hardness2 == 0)
        {
            if (!context.AssumeWalkOnWater && context.CanSprint)
            {
                multiplier *= ActionCosts.SprintMultiplier;
            }
        }
        
        double cost = multiplier * Sqrt2 + hardness1 + hardness2;
        if (ascend)
        {
            cost += ActionCosts.JumpOneBlockCost + context.JumpPenalty;
        }
        else if (descend)
        {
            cost += ActionCosts.WalkOffBlockCost + Math.Max(ActionCosts.FallNBlocksCost[1], ActionCosts.CenterAfterFallCost);
        }
        
        res.X = destX;
        res.Y = y;
        res.Z = destZ;
        res.Cost = cost;
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

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementDiagonal.java:229
        // Complex diagonal movement logic
        var context = new CalculationContext(Baritone);
        if (context.CanSprint)
        {
            state.SetInput(Input.Sprint, true);
        }
        MovementHelper.MoveTowards(Ctx, state, Dest);
        return state;
    }
}

