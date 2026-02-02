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
    private readonly BlockFace _face1;
    private readonly BlockFace _face2;

    public MovementDiagonal(IBaritone baritone, BetterBlockPos src, BlockFace face1, BlockFace face2, int yOffset)
        : base(baritone, src, GetDest(src, face1, face2, yOffset), GetToBreak(src, face1, face2, yOffset), GetToWalkOn(src, face1, face2, yOffset))
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

    private static BetterBlockPos[] GetToBreak(BetterBlockPos src, BlockFace face1, BlockFace face2, int yOffset)
    {
        var dest = GetDest(src, face1, face2, yOffset);
        var diag1 = GetRelative(src, face1);
        var diag2 = GetRelative(src, face2);
        
        var list = new List<BetterBlockPos> { dest, dest.Above() };
        
        if (yOffset > 0)
        {
            list.Add(src.Above(2));
        }
        else if (yOffset < 0)
        {
            list.Add(dest.Above(2));
        }
        
        return list.ToArray();
    }

    private static BetterBlockPos? GetToWalkOn(BetterBlockPos src, BlockFace face1, BlockFace face2, int yOffset)
    {
        return GetDest(src, face1, face2, yOffset).Below();
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
            double cost = MovementHelper.GetMiningDurationTicks(context, destX, y, destZ, destInto, false);
            if (cost >= ActionCosts.CostInf)
            {
                return;
            }
            res.Cost += cost;
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
        bool onClimbable = fromDownName.Contains("ladder", StringComparison.OrdinalIgnoreCase) || 
                          fromDownName.Contains("vine", StringComparison.OrdinalIgnoreCase);
        if (onClimbable)
        {
            multiplier *= 2.0; // Climbable blocks slow movement
        }

        res.Cost += multiplier * Math.Sqrt(2);
        res.X = destX;
        res.Y = descend ? y - 1 : (ascend ? y + 1 : y);
        res.Z = destZ;
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
            // Check for liquid special case
            if (!(MovementHelper.IsLiquid(Ctx, Src) && GetValidPositions().Contains(feet?.Above() ?? Src)))
            {
                return state.SetStatus(MovementStatus.Unreachable);
            }
        }

        if (Dest.Y > Src.Y && ((Entity)Ctx.Player()!).Position.Y < Src.Y + 0.1 && ((Entity)Ctx.Player()!).HorizontalCollision)
        {
            state.SetInput(BaritoneInput.Jump, true);
        }

        if (Sprint())
        {
            state.SetInput(BaritoneInput.Sprint, true);
        }

        var player = Ctx.Player() as Entity;
        if (player != null)
        {
            double diffX = player.Position.X - (Dest.X + 0.5);
            double diffZ = player.Position.Z - (Dest.Z + 0.5);
            double ab = Math.Sqrt(diffX * diffX + diffZ * diffZ);

            if (feet == null || !feet.Equals(Dest) || ab > 0.25)
            {
                MovementHelper.MoveTowards(Ctx, state, Dest);
            }
        }
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

    protected override bool Prepared(MovementState state)
    {
        return true;
    }
}
