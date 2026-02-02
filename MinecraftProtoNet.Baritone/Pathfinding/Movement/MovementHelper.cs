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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Models.World.Meta;
using MinecraftProtoNet.Core.Physics;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Baritone.Pathfinding.Precompute;
using static MinecraftProtoNet.Baritone.Pathfinding.Precompute.Ternary;
using MinecraftProtoNet.Core.Enums;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// Helper methods for movement execution.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java
/// </summary>
public static class MovementHelper
{
    public static readonly BlockFace[] HorizontalsButAlsoDown = 
    {
        BlockFace.North, BlockFace.South, BlockFace.East, BlockFace.West, BlockFace.Bottom
    };

    public static void MoveTowards(IPlayerContext ctx, MovementState state, BetterBlockPos dest)
    {
        var player = ctx.Player() as Entity;
        if (player == null) return;

        var center = new Vector3<double>(dest.X + 0.5, dest.Y, dest.Z + 0.5);
        var playerHead = ctx.PlayerHead();
        var playerRot = ctx.PlayerRotations();
        
        if (playerHead == null || playerRot == null) return;
        
        var rot = RotationUtils.CalcRotationFromVec3d(playerHead, center, playerRot).WithPitch(playerRot.GetPitch());
        
        state.SetTarget(new MovementState.MovementTarget(rot, false));
        state.SetInput(Input.MoveForward, true);

        // Sprinting logic
        if (new CalculationContext(BaritoneAPI.GetProvider().GetPrimaryBaritone()).CanSprint)
        {
            state.SetInput(Input.Sprint, true);
        }
    }

    public static bool CanWalkOn(IPlayerContext ctx, BetterBlockPos pos)
    {
        var state = BlockStateInterface.Get(ctx, pos);
        return CanWalkOn(new CalculationContext(BaritoneAPI.GetProvider().GetPrimaryBaritone()), pos.X, pos.Y, pos.Z, state);
    }

    public static bool CanWalkOn(IPlayerContext ctx, BetterBlockPos pos, BlockState state)
    {
        return CanWalkOn(new CalculationContext(BaritoneAPI.GetProvider().GetPrimaryBaritone()), pos.X, pos.Y, pos.Z, state);
    }

    public static bool CanWalkOn(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        Ternary canWalkOn = CanWalkOnBlockState(state);
        if (canWalkOn == Yes)
        {
            return true;
        }
        if (canWalkOn == No)
        {
            return false;
        }
        return CanWalkOnPosition(bsi, x, y, z, state);
    }

    public static bool CanWalkOn(BlockStateInterface bsi, int x, int y, int z)
    {
        var state = bsi.Get0(x, y, z);
        return CanWalkOn(bsi, x, y, z, state);
    }

    public static bool CanWalkOn(CalculationContext context, int x, int y, int z)
    {
        var state = context.Get(x, y, z);
        return CanWalkOn(context, x, y, z, state);
    }

    public static bool CanWalkOn(CalculationContext context, int x, int y, int z, BlockState state)
    {
        if (state == null) return false;
        
        // Use precomputed data if available
        var pre = context.PrecomputedData;
        return pre.CanWalkOn(context.Bsi, x, y, z, state);
    }

    public static bool CanWalkThrough(IPlayerContext ctx, BetterBlockPos pos)
    {
        return CanWalkThrough(new BlockStateInterface(ctx), pos.X, pos.Y, pos.Z);
    }

    public static bool CanWalkThrough(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        Ternary canWalkThrough = CanWalkThroughBlockState(state);
        if (canWalkThrough == Yes)
        {
            return true;
        }
        if (canWalkThrough == No)
        {
            return false;
        }
        return CanWalkThroughPosition(bsi, x, y, z, state);
    }

    public static bool CanWalkThrough(BlockStateInterface bsi, int x, int y, int z)
    {
        var state = bsi.Get0(x, y, z);
        return CanWalkThrough(bsi, x, y, z, state);
    }

    public static bool CanWalkThrough(CalculationContext context, int x, int y, int z)
    {
        var state = context.Get(x, y, z);
        return CanWalkThrough(context, x, y, z, state);
    }

    public static bool CanWalkThrough(CalculationContext context, int x, int y, int z, BlockState state)
    {
        if (state == null) return true;
        
        var pre = context.PrecomputedData;
        return pre.CanWalkThrough(context.Bsi, x, y, z, state);
    }

    public static double GetMiningDurationTicks(CalculationContext context, int x, int y, int z, bool includeWontBeBreaking)
    {
        var state = context.Get(x, y, z);
        return GetMiningDurationTicks(context, x, y, z, state, includeWontBeBreaking);
    }

    public static double GetMiningDurationTicks(CalculationContext context, int x, int y, int z, BlockState state, bool includeWontBeBreaking)
    {
        if (state == null || state.IsAir) return 0;
        
        if (state.DestroySpeed < 0) return ActionCosts.CostInf;
        
        if (!context.AllowBreak && !context.AllowBreakAnyway.Contains(state.Name))
        {
            return includeWontBeBreaking ? 0 : ActionCosts.CostInf;
        }

        if (context.IsPossiblyProtected(x, y, z))
        {
            return ActionCosts.CostInf;
        }

        double speed = context.ToolSet.GetStrVsBlock(state);
        if (speed <= 0) return ActionCosts.CostInf;

        double hardness = state.DestroySpeed;
        double ticks = (hardness * 1.5) / speed;
        
        if (ticks > 12000) return ActionCosts.CostInf; 

        return ticks + context.BreakBlockAdditionalCost;
    }

    public static bool IsWater(BlockState state)
    {
        return state != null && state.Name.Contains("water", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLava(BlockState state)
    {
        return state != null && state.Name.Contains("lava", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLiquid(BlockState state)
    {
        return state != null && state.IsLiquid;
    }

    public static bool IsLiquid(IPlayerContext ctx, BetterBlockPos pos)
    {
        var state = BlockStateInterface.Get(ctx, pos);
        return IsLiquid(state);
    }

    public static bool IsBottomSlab(BlockState state)
    {
        if (state == null) return false;
        return state.Name.Contains("slab", StringComparison.OrdinalIgnoreCase) && 
               state.Properties.TryGetValue("type", out var type) && type == "bottom";
    }

    public static bool IsBlockNormalCube(BlockState state)
    {
        return state != null && !state.IsAir && state.HasCollision;
    }

    public static bool IsReplaceable(int x, int y, int z, BlockState state, BlockStateInterface bsi)
    {
        if (state == null) return true;
        return state.IsAir || IsWater(state) || IsLava(state) || state.Name.Contains("grass", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanPlaceAgainst(BlockStateInterface bsi, int x, int y, int z)
    {
        var state = bsi.Get0(x, y, z);
        if (state == null || state.IsAir) return false;
        return state.HasCollision || state.Name.Contains("glass", StringComparison.OrdinalIgnoreCase);
    }

    public static double GetHorizontalDistance(BetterBlockPos a, BetterBlockPos b)
    {
        double dx = a.X - b.X;
        double dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    public static double GetDistance(BetterBlockPos a, BetterBlockPos b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public static bool IsAvoidBlock(BlockState state)
    {
        if (state == null) return false;
        string name = state.Name.ToLower();
        return name.Contains("fire") || name.Contains("cactus") || name.Contains("magma") || name.Contains("berry_bush");
    }

    public static void SwitchToBestToolFor(IPlayerContext ctx, BlockState state)
    {
        var player = ctx.Player() as Entity;
        if (player == null) return;

        var toolSet = new ToolSet(player);
        int bestSlot = toolSet.GetBestSlot(state.Name, false);
        player.HeldSlot = (short)bestSlot;
    }

    public static bool MustBeSolidToWalkOn(CalculationContext context, int x, int y, int z, BlockState state)
    {
        if (state == null) return false;
        return state.HasCollision && !IsLiquid(state);
    }

    public static bool CanUseFrostWalker(CalculationContext context, BetterBlockPos pos)
    {
        return CanUseFrostWalker(context, pos.X, pos.Y, pos.Z);
    }

    public static bool CanUseFrostWalker(CalculationContext context, int x, int y, int z)
    {
        var state = context.Get(x, y, z);
        return CanUseFrostWalker(context, state);
    }

    public static bool CanUseFrostWalker(CalculationContext context, BlockState state)
    {
        return context.FrostWalker > 0 && IsWater(state) && 
               state.Properties.TryGetValue("level", out var level) && level == "0";
    }

    public static bool CanUseFrostWalker(CalculationContext context, BlockState state, int x, int y, int z)
    {
        return CanUseFrostWalker(context, state);
    }

    public static bool AvoidBreaking(BlockState state)
    {
        var settings = BaritoneAPI.GetSettings();
        return settings.BlocksToAvoidBreaking.Value.Contains(state.Name);
    }

    public static bool AvoidBreaking(CalculationContext context, int x, int y, int z, BlockState state)
    {
        return AvoidBreaking(state);
    }

    public static bool AvoidBreaking(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        return AvoidBreaking(state);
    }

    public static bool IsTransparent(BlockState state)
    {
        return state.IsAir || state.Name.Contains("glass", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTransparent(object obj)
    {
        if (obj is BlockState state) return IsTransparent(state);
        return false;
    }

    public static bool FullyPassable(BlockState state)
    {
        return !state.HasCollision;
    }

    public static bool FullyPassable(IPlayerContext ctx, BetterBlockPos pos)
    {
        return FullyPassable(new CalculationContext(BaritoneAPI.GetProvider().GetPrimaryBaritone()), pos);
    }

    public static bool FullyPassable(CalculationContext context, BetterBlockPos pos)
    {
        return FullyPassable(context, pos.X, pos.Y, pos.Z);
    }

    public static bool FullyPassable(CalculationContext context, int x, int y, int z)
    {
        return FullyPassable(context, x, y, z, context.Get(x, y, z));
    }

    public static bool FullyPassable(CalculationContext context, int x, int y, int z, BlockState state)
    {
        return FullyPassable(state);
    }

    public static bool FullyPassable(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        return FullyPassable(state);
    }

    public static bool AvoidWalkingInto(BlockState state)
    {
        return IsAvoidBlock(state);
    }

    public static bool AvoidWalkingInto(CalculationContext context, int x, int y, int z, BlockState state)
    {
        return AvoidWalkingInto(state);
    }

    public static bool IsFlowing(BlockState state)
    {
        return state.IsLiquid && state.Properties.TryGetValue("level", out var level) && level != "0";
    }

    public static bool IsFlowing(CalculationContext context, int x, int y, int z, BlockState state)
    {
        return IsFlowing(state);
    }

    public static bool IsFlowing(int x, int y, int z, BlockState state, BlockStateInterface bsi)
    {
        return IsFlowing(state);
    }

    public static Ternary CanWalkOnBlockState(BlockState state)
    {
        if (state == null) return No;
        if (state.IsAir) return No;
        if (state.IsLiquid) return Maybe;
        return state.HasCollision ? Yes : No;
    }

    public static Ternary CanWalkThroughBlockState(BlockState state)
    {
        if (state == null) return Yes;
        if (state.IsAir) return Yes;
        if (state.IsLiquid) return No;
        return state.HasCollision ? No : Yes;
    }

    public static Ternary FullyPassableBlockState(BlockState state)
    {
        return CanWalkThroughBlockState(state);
    }

    public static bool CanWalkOnPosition(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        return state.HasCollision;
    }

    public static bool CanWalkThroughPosition(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        return !state.HasCollision;
    }

    public static bool FullyPassablePosition(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        return !state.HasCollision;
    }

    public enum PlaceResult
    {
        ReadyToPlace,
        Attempting,
        NoOption
    }

    public static PlaceResult AttemptToPlaceABlock(MovementState state, IBaritone baritone, BetterBlockPos pos, bool preferDown, bool wouldSneak)
    {
        var ctx = baritone.GetPlayerContext();
        var direct = RotationUtils.Reachable(ctx, pos, wouldSneak);
        bool found = false;
        
        var bsi = new BlockStateInterface(ctx);
        
        // Try placing against adjacent blocks
        for (int i = 0; i < HorizontalsButAlsoDown.Length; i++)
        {
            var direction = HorizontalsButAlsoDown[i];
            BetterBlockPos against1 = direction switch
            {
                BlockFace.North => pos.North(),
                BlockFace.South => pos.South(),
                BlockFace.East => pos.East(),
                BlockFace.West => pos.West(),
                BlockFace.Bottom => pos.Below(),
                _ => pos
            };
            
            if (CanPlaceAgainst(bsi, against1.X, against1.Y, against1.Z))
            {
                var inventoryBehavior = ((Core.Baritone)baritone).GetInventoryBehavior();
                if (!inventoryBehavior.SelectThrowawayForLocation(false, pos.X, pos.Y, pos.Z))
                {
                    state.SetStatus(Api.Pathing.Movement.MovementStatus.Unreachable);
                    return PlaceResult.NoOption;
                }
                
                double faceX = (pos.X + against1.X + 1.0) * 0.5;
                double faceY = (pos.Y + against1.Y + 0.5) * 0.5;
                double faceZ = (pos.Z + against1.Z + 1.0) * 0.5;
                
                if (pos.X == against1.X && pos.Z == against1.Z)
                {
                    faceX += 0.05;
                    faceZ += 0.05;
                }
                
                var facePos = new Vector3<double>(faceX, faceY, faceZ);
                
                var player = ctx.Player() as Entity;
                if (player == null) continue;
                
                var playerHead = ctx.PlayerHead();
                var playerRot = ctx.PlayerRotations();
                if (playerHead == null || playerRot == null) continue;
                Vector3<double> eyes = wouldSneak ? RayTraceUtils.InferSneakingEyePosition(player) : playerHead;
                Rotation place = RotationUtils.CalcRotationFromVec3d(eyes, facePos, playerRot);
                Rotation actual = baritone.GetLookBehavior().GetAimProcessor().PeekRotation(place);
                
                var world = ctx.World() as Level;
                var result = RayTraceUtils.RayTraceTowards(player, world, actual, ctx.PlayerController().GetBlockReachDistance(), wouldSneak);
                if (result != null && result.Block != null)
                {
                    var hitPos = result.BlockPosition;
                    if (hitPos.X == against1.X && hitPos.Y == against1.Y && hitPos.Z == against1.Z)
                    {
                        var adjacentPos = result.GetAdjacentBlockPosition();
                        if (adjacentPos.X == pos.X && adjacentPos.Y == pos.Y && adjacentPos.Z == pos.Z)
                        {
                            baritone.GetGameEventHandler().LogDirect($"Attempting placement at {pos} against {against1}");
                            state.SetTarget(new MovementState.MovementTarget(place, true));
                            found = true;
                            
                            if (!preferDown)
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }
        
        var objectMouseOver = ctx.ObjectMouseOver();
        if (objectMouseOver is RaycastHit hit)
        {
            var selectedBlock = new BetterBlockPos(hit.BlockPosition.X, hit.BlockPosition.Y, hit.BlockPosition.Z);
            var adjacentPos = BetterBlockPos.From((hit.GetAdjacentBlockPosition().X, hit.GetAdjacentBlockPosition().Y, hit.GetAdjacentBlockPosition().Z));
            
            if (selectedBlock.Equals(pos) || (CanPlaceAgainst(bsi, selectedBlock.X, selectedBlock.Y, selectedBlock.Z) && adjacentPos != null && adjacentPos.Equals(pos)))
            {
                if (wouldSneak)
                {
                    state.SetInput(Input.Sneak, true);
                }
                var inventoryBehavior = ((Core.Baritone)baritone).GetInventoryBehavior();
                inventoryBehavior.SelectThrowawayForLocation(true, pos.X, pos.Y, pos.Z);
                return PlaceResult.ReadyToPlace;
            }
        }
        
        if (found)
        {
            if (wouldSneak)
            {
                state.SetInput(Input.Sneak, true);
            }
            var inventoryBehavior = ((Core.Baritone)baritone).GetInventoryBehavior();
            inventoryBehavior.SelectThrowawayForLocation(true, pos.X, pos.Y, pos.Z);
            return PlaceResult.Attempting;
        }
        
        return PlaceResult.NoOption;
    }
}
