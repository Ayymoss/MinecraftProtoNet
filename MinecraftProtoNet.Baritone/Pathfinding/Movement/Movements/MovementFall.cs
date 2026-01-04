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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementFall.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.State;
using BaritoneInput = MinecraftProtoNet.Baritone.Api.Utils.Input.Input;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for falling multiple blocks.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementFall.java
/// </summary>
public class MovementFall(IBaritone baritone, BetterBlockPos src, BetterBlockPos dest)
    : Movement(baritone, src, dest, BuildPositionsToBreak(src, dest))
{
    public override double CalculateCost(CalculationContext context)
    {
        var result = new Baritone.Utils.Pathing.MutableMoveResult();
        MovementDescend.Cost(context, Src.X, Src.Y, Src.Z, Dest.X, Dest.Z, result);
        if (result.Y != Dest.Y)
        {
            return ActionCosts.CostInf; // doesn't apply to us, this position is a descend not a fall
        }
        return result.Cost;
    }

    protected override HashSet<BetterBlockPos> CalculateValidPositions()
    {
        var set = new HashSet<BetterBlockPos> { Src };
        for (int y = Src.Y - Dest.Y; y >= 0; y--)
        {
            set.Add(Dest.Above(y));
        }
        return set;
    }

    private bool WillPlaceBucket()
    {
        var context = new CalculationContext(Baritone);
        var result = new Baritone.Utils.Pathing.MutableMoveResult();
        return MovementDescend.DynamicFallCost(context, Src.X, Src.Y, Src.Z, Dest.X, Dest.Z, 0, 
            context.Get(Dest.X, Src.Y - 2, Dest.Z), result);
    }

    protected override MovementState UpdateState(MovementState state)
    {
        base.UpdateState(state);
        if (state.GetStatus() != MovementStatus.Running)
        {
            return state;
        }

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementFall.java:87-160
        var playerFeet = Ctx.PlayerFeet();
        var player = Ctx.Player() as Entity;
        if (player == null) return state;
        
        var playerHead = Ctx.PlayerHead();
        var playerRot = Ctx.PlayerRotations();
        if (playerHead == null || playerRot == null) return state;
        var toDest = Utils.RotationUtils.CalcRotationFromVec3d(
            playerHead,
            Utils.VecUtils.GetBlockPosCenter(Dest),
            playerRot
        );
        Rotation? targetRotation = null;
        
        var world = Ctx.World() as Level;
        if (world != null)
        {
            var destState = world.GetBlockAt(Dest.X, Dest.Y, Dest.Z);
            bool isWater = destState != null && MovementHelper.IsWater(destState);
            bool willPlace = WillPlaceBucket();
            
            if (!isWater && willPlace && !playerFeet?.Equals(Dest) == true)
            {
                var context = new CalculationContext(Baritone);
                if (!context.HasWaterBucket)
                {
                    return state.SetStatus(MovementStatus.Unreachable);
                }
                
                // Select water bucket and aim down
                // TODO: Select water bucket slot when item registry is available
                targetRotation = new Rotation(toDest.GetYaw(), 90.0f);
                
                if (Ctx.IsLookingAt(Dest) || Ctx.IsLookingAt(Dest.Below()))
                {
                    state.SetInput(BaritoneInput.ClickRight, true);
                }
            }
            
            if (targetRotation != null)
            {
                state.SetTarget(new MovementState.MovementTarget(targetRotation, true));
            }
            else
            {
                state.SetTarget(new MovementState.MovementTarget(toDest, false));
            }
            
            if (playerFeet != null && playerFeet.Equals(Dest))
            {
                double yDiff = player.Position.Y - playerFeet.Y;
                if (yDiff < 0.094 || isWater) // 0.094 because lilypads
                {
                    if (isWater)
                    {
                        // Try to pick up water with empty bucket
                        // TODO: Select empty bucket slot when item registry is available
                        if (player.Velocity.Y >= 0)
                        {
                            state.SetInput(BaritoneInput.ClickRight, true);
                        }
                        return state;
                    }
                    else
                    {
                        return state.SetStatus(MovementStatus.Success);
                    }
                }
            }
            
            // Movement towards destination with avoidance logic
            var destCenter = Utils.VecUtils.GetBlockPosCenter(Dest);
            double dx = Math.Abs(player.Position.X + player.Velocity.X - destCenter.X);
            double dz = Math.Abs(player.Position.Z + player.Velocity.Z - destCenter.Z);
            
            if (dx > 0.1 || dz > 0.1)
            {
                if (!player.IsOnGround && Math.Abs(player.Velocity.Y) > 0.4)
                {
                    state.SetInput(BaritoneInput.Sneak, true);
                }
                state.SetInput(BaritoneInput.MoveForward, true);
            }
            
            // Avoid ladders
            BetterBlockPos? avoidDir = null;
            for (int i = 0; i < 15; i++)
            {
                var belowState = world.GetBlockAt(playerFeet?.X ?? 0, (playerFeet?.Y ?? 0) - i, playerFeet?.Z ?? 0);
                if (belowState != null && belowState.Name.Contains("ladder", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: Get ladder facing direction when block properties are available
                    break;
                }
            }
            
            if (targetRotation == null && playerFeet != null)
            {
                var avoidOffset = avoidDir != null ? new Vector3<double>(avoidDir.X * 0.125, 0, avoidDir.Z * 0.125) : new Vector3<double>(0, 0, 0);
                var destCenterOffset = new Vector3<double>(destCenter.X + avoidOffset.X, destCenter.Y, destCenter.Z + avoidOffset.Z);
                var playerHead2 = Ctx.PlayerHead();
                var playerRot2 = Ctx.PlayerRotations();
                if (playerHead2 != null && playerRot2 != null)
                {
                    state.SetTarget(new MovementState.MovementTarget(
                        Utils.RotationUtils.CalcRotationFromVec3d(playerHead2, destCenterOffset, playerRot2),
                        false
                    ));
                }
            }
        }
        
        return state;
    }

    protected override bool SafeToCancel(MovementState state)
    {
        var feet = Ctx.PlayerFeet();
        return feet != null && (feet.Equals(Src) || state.GetStatus() != MovementStatus.Running);
    }

    protected override bool Prepared(MovementState state)
    {
        if (state.GetStatus() == MovementStatus.Waiting)
        {
            return true;
        }
        // Only break if one of the first three needs to be broken
        for (int i = 0; i < 4 && i < PositionsToBreak.Length; i++)
        {
            if (!MovementHelper.CanWalkThrough(Ctx, PositionsToBreak[i]))
            {
                return base.Prepared(state);
            }
        }
        return true;
    }

    private static BetterBlockPos[] BuildPositionsToBreak(BetterBlockPos src, BetterBlockPos dest)
    {
        int diffX = src.X - dest.X;
        int diffZ = src.Z - dest.Z;
        int diffY = Math.Abs(src.Y - dest.Y);
        var toBreak = new BetterBlockPos[diffY + 2];
        for (int i = 0; i < toBreak.Length; i++)
        {
            toBreak[i] = new BetterBlockPos(src.X - diffX, src.Y + 1 - i, src.Z - diffZ);
        }
        return toBreak;
    }
}

