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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Baritone.Utils.Pathing;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Physics;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for parkour jumps (jumping across gaps).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java
/// </summary>
public class MovementParkour : Movement
{
    private static readonly BetterBlockPos[] Empty = [];

    private readonly BlockFace _direction;
    private readonly int _dist;
    private readonly bool _ascend;

    private MovementParkour(IBaritone baritone, BetterBlockPos src, int dist, BlockFace dir, bool ascend)
        : base(baritone, src, GetDest(src, dist, dir, ascend), Empty, GetPlacePos(src, dist, dir, ascend))
    {
        _direction = dir;
        _dist = dist;
        _ascend = ascend;
    }

    private static BetterBlockPos GetDest(BetterBlockPos src, int dist, BlockFace dir, bool ascend)
    {
        var normal = Direction.GetNormal(dir);
        return new BetterBlockPos(src.X + normal.X * dist, src.Y + (ascend ? 1 : 0), src.Z + normal.Z * dist);
    }

    private static BetterBlockPos? GetPlacePos(BetterBlockPos src, int dist, BlockFace dir, bool ascend)
    {
        var normal = Direction.GetNormal(dir);
        return new BetterBlockPos(src.X + normal.X * dist, src.Y - (ascend ? 0 : 1), src.Z + normal.Z * dist);
    }

    public static MovementParkour Cost(CalculationContext context, BetterBlockPos src, BlockFace direction)
    {
        var res = new MutableMoveResult();
        Cost(context, src.X, src.Y, src.Z, direction, res);
        int dist = Math.Abs(res.X - src.X) + Math.Abs(res.Z - src.Z);
        return new MovementParkour(context.GetBaritone(), src, dist, direction, res.Y > src.Y);
    }

    public static void Cost(CalculationContext context, int x, int y, int z, BlockFace dir, MutableMoveResult res)
    {
        if (!context.AllowParkour)
        {
            return;
        }
        if (!context.AllowJumpAtBuildLimit && y >= context.World.DimensionType.Height)
        {
            return;
        }
        
        var normal = Direction.GetNormal(dir);
        int xDiff = normal.X;
        int zDiff = normal.Z;
        
        if (!MovementHelper.FullyPassable(context, x + xDiff, y, z + zDiff))
        {
            return;
        }
        
        var adj = context.Get(x + xDiff, y - 1, z + zDiff);
        if (MovementHelper.CanWalkOn(context, x + xDiff, y - 1, z + zDiff, adj))
        {
            return; // don't parkour if we could just traverse
        }
        if (MovementHelper.AvoidWalkingInto(adj) && !MovementHelper.IsWater(adj))
        {
            return;
        }
        if (!MovementHelper.FullyPassable(context, x + xDiff, y + 1, z + zDiff))
        {
            return;
        }
        if (!MovementHelper.FullyPassable(context, x + xDiff, y + 2, z + zDiff))
        {
            return;
        }
        if (!MovementHelper.FullyPassable(context, x, y + 2, z))
        {
            return;
        }
        
        var standingOn = context.Get(x, y - 1, z);
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementParkour.java:114
        // Check for vine, ladder, stair, bottom slab
        string standingOnName = standingOn.Name;
        bool isClimbable = standingOnName.Contains("vine", StringComparison.OrdinalIgnoreCase) ||
                          standingOnName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                          standingOn.IsStairs ||
                          (standingOn.IsSlab && !standingOn.IsTop);
        if (isClimbable)
        {
            return; // Can't parkour from climbable blocks
        }
        if (context.AssumeWalkOnWater && standingOn.IsLiquid)
        {
            return;
        }
        if (context.Get(x, y, z).IsLiquid)
        {
            return; // can't jump out of water
        }
        
        int maxJump;
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java:104-113
        bool onSoulSand = standingOnName.Contains("soul_sand", StringComparison.OrdinalIgnoreCase);
        if (onSoulSand)
        {
            maxJump = 2; // 1 block gap
        }
        else if (context.CanSprint)
        {
            maxJump = 4;
        }
        else
        {
            maxJump = 3;
        }

        // Check parkour jumps from smallest to largest for obstacles/walls and landing positions
        int verifiedMaxJump = 1;
        for (int i = 2; i <= maxJump; i++)
        {
            int destX = x + xDiff * i;
            int destZ = z + zDiff * i;

            // Check head/feet
            if (!MovementHelper.FullyPassable(context, destX, y + 1, destZ))
            {
                break;
            }
            if (!MovementHelper.FullyPassable(context, destX, y + 2, destZ))
            {
                break;
            }

            // Check for ascend landing position
            var destInto = context.Bsi.Get0(destX, y, destZ);
            if (!MovementHelper.FullyPassable(context, destX, y, destZ, destInto))
            {
                if (i <= 3 && context.AllowParkourAscend && context.CanSprint && 
                    MovementHelper.CanWalkOn(context, destX, y, destZ, destInto) && 
                    CheckOvershootSafety(context.Bsi, destX + xDiff, y + 1, destZ + zDiff))
                {
                    res.X = destX;
                    res.Y = y + 1;
                    res.Z = destZ;
                    res.Cost = i * ActionCosts.SprintOneBlockCost + context.JumpPenalty;
                    return;
                }
                break;
            }

            // Check for flat landing position
            var landingOn = context.Bsi.Get0(destX, y - 1, destZ);
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java:143-157
            // farmland needs to be canWalkOn otherwise farm can never work at all, but we want to specifically disallow ending a jump on farmland
            // frostwalker works here because we can't jump from possibly unfrozen water
            string landingOnName = landingOn.Name;
            bool isFarmland = landingOnName.Contains("farmland", StringComparison.OrdinalIgnoreCase);
            bool canUseFrostWalker = Math.Min(16, context.FrostWalker + 2) >= i && MovementHelper.CanUseFrostWalker(context, landingOn);
            if ((!isFarmland && MovementHelper.CanWalkOn(context, destX, y - 1, destZ, landingOn)) || canUseFrostWalker)
            {
                if (CheckOvershootSafety(context.Bsi, destX + xDiff, y, destZ + zDiff))
                {
                    res.X = destX;
                    res.Y = y;
                    res.Z = destZ;
                    res.Cost = CostFromJumpDistance(i) + context.JumpPenalty;
                    return;
                }
                break; // Reference: MovementParkour.java:156 — break if landing found but overshoot unsafe
            }

            if (!MovementHelper.FullyPassable(context, destX, y + 3, destZ))
            {
                break;
            }

            verifiedMaxJump = i;
        }

        // Parkour place logic (if enabled)
        if (!context.AllowParkourPlace)
        {
            return;
        }
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java:166-200
        // Check parkour jumps from largest to smallest for positions to place blocks
        for (int i = verifiedMaxJump; i > 1; i--)
        {
            int destX = x + xDiff * i;
            int destZ = z + zDiff * i;
            var toReplace = context.Get(destX, y - 1, destZ);
            double placeCost = context.CostOfPlacingAt(destX, y - 1, destZ, toReplace);
            if (placeCost >= ActionCosts.CostInf)
            {
                continue;
            }
            if (!MovementHelper.IsReplaceable(destX, y - 1, destZ, toReplace, context.Bsi))
            {
                continue;
            }
            if (!CheckOvershootSafety(context.Bsi, destX + xDiff, y, destZ + zDiff))
            {
                continue;
            }
            for (int j = 0; j < 5; j++)
            {
                var dir2 = Movement.HorizontalsButAlsoDown[j];
                var normal2 = Direction.GetNormal(dir2);
                int againstX = destX + normal2.X;
                int againstY = y - 1 + normal2.Y;
                int againstZ = destZ + normal2.Z;
                if (againstX == destX - xDiff && againstZ == destZ - zDiff) // we can't turn around that fast
                {
                    continue;
                }
                if (MovementHelper.CanPlaceAgainst(context.Bsi, againstX, againstY, againstZ))
                {
                    res.X = destX;
                    res.Y = y;
                    res.Z = destZ;
                    res.Cost = CostFromJumpDistance(i) + placeCost + context.JumpPenalty;
                    return;
                }
            }
        }
    }

    private static bool CheckOvershootSafety(BlockStateInterface bsi, int x, int y, int z)
    {
        // Check if we can safely overshoot the landing position
        return !MovementHelper.AvoidWalkingInto(bsi.Get0(x, y, z)) && 
               !MovementHelper.AvoidWalkingInto(bsi.Get0(x, y + 1, z));
    }

    private static double CostFromJumpDistance(int dist)
    {
        return dist switch
        {
            2 => ActionCosts.WalkOneBlockCost * 2,
            3 => ActionCosts.WalkOneBlockCost * 3,
            4 => ActionCosts.SprintOneBlockCost * 4,
            _ => throw new InvalidOperationException($"Invalid jump distance: {dist}")
        };
    }

    protected override HashSet<BetterBlockPos> CalculateValidPositions()
    {
        var set = new HashSet<BetterBlockPos>();
        for (int i = 0; i <= _dist; i++)
        {
            for (int y = 0; y < 2; y++)
            {
                var normal = Direction.GetNormal(_direction);
                set.Add(new BetterBlockPos(Src.X + normal.X * i, Src.Y + y, Src.Z + normal.Z * i));
            }
        }
        return set;
    }

    public override double CalculateCost(CalculationContext context)
    {
        var res = new MutableMoveResult();
        Cost(context, Src.X, Src.Y, Src.Z, _direction, res);
        if (res.X != Dest.X || res.Y != Dest.Y || res.Z != Dest.Z)
        {
            return ActionCosts.CostInf;
        }
        return res.Cost;
    }

    protected override bool SafeToCancel(MovementState state)
    {
        // Once this movement is instantiated, the state is default to PREPPING
        // but once it's ticked for the first time it changes to RUNNING
        // since we don't really know anything about momentum, it suffices to say Parkour can only be canceled on the 0th tick
        return state.GetStatus() != MovementStatus.Running;
    }

    public override MovementState UpdateState(MovementState state)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java:252-308
        base.UpdateState(state);
        if (state.GetStatus() != MovementStatus.Running)
        {
            return state;
        }

        var feet = Ctx.PlayerFeet();
        if (feet != null && feet.Y < Src.Y)
        {
            // we have fallen
            return state.SetStatus(MovementStatus.Unreachable);
        }

        if (_dist >= 4 || _ascend)
        {
            state.SetInput(Input.Sprint, true);
        }

        MovementHelper.MoveTowards(Ctx, state, Dest);

        var player = Ctx.Player() as Entity;

        if (feet != null && feet.Equals(Dest))
        {
            // Reference: MovementParkour.java:267-275
            var destBlock = BlockStateInterface.Get(Ctx, Dest);
            string destName = destBlock.Name;
            if (destName.Contains("vine", StringComparison.OrdinalIgnoreCase) ||
                destName.Contains("ladder", StringComparison.OrdinalIgnoreCase))
            {
                // it physically hurt me to add support for parkour jumping onto a vine
                // but i did it anyway
                return state.SetStatus(MovementStatus.Success);
            }
            if (player != null && player.Position.Y - feet.Y < 0.094) // lilypads
            {
                state.SetStatus(MovementStatus.Success);
            }
        }
        else if (feet != null && !feet.Equals(Src))
        {
            var normal = Direction.GetNormal(_direction);
            var srcRelative = new BetterBlockPos(Src.X + normal.X, Src.Y, Src.Z + normal.Z);

            if (feet.Equals(srcRelative) || (player != null && player.Position.Y - Src.Y > 0.0001))
            {
                // Reference: MovementParkour.java:278-285 — parkour place mid-air
                if (Core.Baritone.Settings().AllowPlace.Value
                    && Baritone.GetInventoryBehavior().HasGenericThrowaway()
                    && !MovementHelper.CanWalkOn(Ctx, Dest.Below())
                    && player != null && !player.IsOnGround
                    && MovementHelper.AttemptToPlaceABlock(state, Baritone, Dest.Below(), true, false) == MovementHelper.PlaceResult.ReadyToPlace)
                {
                    // go in the opposite order to check DOWN before all horizontals -- down is preferable
                    // because you don't have to look to the side while in midair, which could mess up the trajectory
                    state.SetInput(Input.ClickRight, true);
                }

                // Reference: MovementParkour.java:288-295 — prevent jumping too late by checking for ascend
                if (_dist == 3 && !_ascend) // this is a 2 block gap, dest = src + direction * 3
                {
                    double xDiff = (Src.X + 0.5) - (player?.Position.X ?? 0);
                    double zDiff = (Src.Z + 0.5) - (player?.Position.Z ?? 0);
                    double distFromStart = Math.Max(Math.Abs(xDiff), Math.Abs(zDiff));
                    if (distFromStart < 0.7)
                    {
                        return state;
                    }
                }

                state.SetInput(Input.Jump, true);
            }
            else
            {
                // Reference: MovementParkour.java:298-305 — not in expected position, stop and reposition
                var destRelativeBack = new BetterBlockPos(Dest.X - normal.X, Dest.Y, Dest.Z - normal.Z);
                if (!feet.Equals(destRelativeBack))
                {
                    state.SetInput(Input.Sprint, false);
                    var srcRelativeBack = new BetterBlockPos(Src.X - normal.X, Src.Y, Src.Z - normal.Z);
                    if (feet.Equals(srcRelativeBack))
                    {
                        MovementHelper.MoveTowards(Ctx, state, Src);
                    }
                    else
                    {
                        MovementHelper.MoveTowards(Ctx, state, srcRelativeBack);
                    }
                }
            }
        }

        return state;
    }
}

