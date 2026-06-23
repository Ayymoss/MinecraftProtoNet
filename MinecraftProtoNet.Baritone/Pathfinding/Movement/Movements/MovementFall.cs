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
using MinecraftProtoNet.Core.Physics;
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

    public override MovementState UpdateState(MovementState state)
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
            // Reference: MovementFall.java:98-100 - sneak when standing on a magma block and all stepping
            // blocks are walk-through (so we won't be held in place by a wall).
            if (playerFeet != null
                && BlockStateInterface.Get(Ctx, playerFeet.Below()).IsMagmaBlock
                && MovementHelper.SteppingOnBlocks(Ctx).All(b => MovementHelper.CanWalkThrough(Ctx, b)))
            {
                state.SetInput(BaritoneInput.Sneak, true);
            }

            var destState = world.GetBlockAt(Dest.X, Dest.Y, Dest.Z);
            bool isWater = destState != null && MovementHelper.IsWater(destState);
            bool willPlace = WillPlaceBucket();

            // Reference: MovementFall.java:103-117 - MLG water-bucket clutch
            if (!isWater && willPlace && playerFeet != null && !playerFeet.Equals(Dest))
            {
                var context = new CalculationContext(Baritone);
                // context.HasWaterBucket is false in the Nether (CheckWaterBucket guards the dimension),
                // which subsumes java's explicit `dimension() == NETHER` check.
                if (!context.HasWaterBucket)
                {
                    return state.SetStatus(MovementStatus.Unreachable);
                }

                // Only aim down + place once within reach of dest AND actually airborne (java :108).
                // Without this gate the bot aims the bucket too early, while still falling from far above.
                if (player.Position.Y - Dest.Y < Ctx.PlayerController().GetBlockReachDistance() && !player.IsOnGround)
                {
                    // TODO: select the water bucket hotbar slot (item-selection infra)
                    targetRotation = new Rotation(toDest.GetYaw(), 90.0f);

                    if (Ctx.IsLookingAt(Dest) || Ctx.IsLookingAt(Dest.Below()))
                    {
                        state.SetInput(BaritoneInput.ClickRight, true);
                    }
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
            
            // Reference: MovementFall.java:123-140
            if (playerFeet != null && playerFeet.Equals(Dest)
                && (player.Position.Y - playerFeet.Y < 0.094 || isWater)) // 0.094 because lilypads
            {
                if (isWater) // only still water — flowing water can't be picked up with a bucket
                {
                    if (HasEmptyBucketInHotbar())
                    {
                        // TODO: select the empty bucket hotbar slot (item-selection infra)
                        if (player.Velocity.Y >= 0)
                        {
                            return state.SetInput(BaritoneInput.ClickRight, true);
                        }
                        return state;
                    }
                    else if (player.Velocity.Y >= 0)
                    {
                        // No empty bucket to recover — once we've stopped sinking, the fall is done.
                        return state.SetStatus(MovementStatus.Success);
                    }
                    // else: no bucket and still sinking — fall through to stay centered (this water may be flowing under the surface)
                }
                else
                {
                    return state.SetStatus(MovementStatus.Success);
                }
            }
            
            // Movement towards destination with avoidance logic
            var destCenter = Utils.VecUtils.GetBlockPosCenter(Dest);
            if (Math.Abs(player.Position.X + player.Velocity.X - destCenter.X) > 0.1 || Math.Abs(player.Position.Z + player.Velocity.Z - destCenter.Z) > 0.1)
            {
                if (!player.IsOnGround && Math.Abs(player.Velocity.Y) > 0.4)
                {
                    state.SetInput(BaritoneInput.Sneak, true);
                }
                state.SetInput(BaritoneInput.MoveForward, true);
            }

            // Reference: MovementFall.java:148-162 - bias away from an adjacent ladder while falling
            var avoidDir = Avoid();
            Vector3<int> avoid;
            if (avoidDir == null)
            {
                avoid = new Vector3<int>(Src.X - Dest.X, Src.Y - Dest.Y, Src.Z - Dest.Z); // src.subtract(dest); only x/z used
            }
            else
            {
                var n = Direction.GetNormal(avoidDir.Value);
                avoid = new Vector3<int>(n.X, n.Y, n.Z);
                double dist = Math.Abs(avoid.X * (destCenter.X - avoid.X / 2.0 - player.Position.X))
                            + Math.Abs(avoid.Z * (destCenter.Z - avoid.Z / 2.0 - player.Position.Z));
                if (dist < 0.6)
                {
                    state.SetInput(BaritoneInput.MoveForward, true);
                }
                else if (!player.IsOnGround)
                {
                    state.SetInput(BaritoneInput.Sneak, false);
                }
            }

            if (targetRotation == null)
            {
                var destCenterOffset = new Vector3<double>(destCenter.X + 0.125 * avoid.X, destCenter.Y, destCenter.Z + 0.125 * avoid.Z);
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

    // Reference: MovementFall.java:166-174 - if falling alongside a ladder, return its FACING direction
    private MinecraftProtoNet.Core.Enums.BlockFace? Avoid()
    {
        var world = Ctx.World() as Level;
        if (world == null) return null;
        var feet = Ctx.PlayerFeet();
        if (feet == null) return null;
        for (int i = 0; i < 15; i++)
        {
            var blockState = world.GetBlockAt(feet.X, feet.Y - i, feet.Z);
            if (blockState != null && blockState.IsLadder)
            {
                if (blockState.Properties.TryGetValue("facing", out var facing))
                {
                    return facing.ToLowerInvariant() switch
                    {
                        "north" => MinecraftProtoNet.Core.Enums.BlockFace.North,
                        "south" => MinecraftProtoNet.Core.Enums.BlockFace.South,
                        "west" => MinecraftProtoNet.Core.Enums.BlockFace.West,
                        "east" => MinecraftProtoNet.Core.Enums.BlockFace.East,
                        _ => (MinecraftProtoNet.Core.Enums.BlockFace?)null
                    };
                }
                return null;
            }
        }
        return null;
    }

    // Reference: MovementFall.java:125 - is an empty bucket available in the hotbar (to recover the MLG water)
    private bool HasEmptyBucketInHotbar()
    {
        var player = Ctx.Player() as Entity;
        if (player == null) return false;
        var itemRegistry = Baritone.GetItemRegistryService();
        for (int i = 36; i <= 44; i++)
        {
            var slot = player.Inventory.GetSlot((short)i);
            if (slot.ItemId != null && slot.ItemCount > 0)
            {
                var name = itemRegistry.GetItemName(slot.ItemId.Value);
                if (name != null && name.Equals("minecraft:bucket", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
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

