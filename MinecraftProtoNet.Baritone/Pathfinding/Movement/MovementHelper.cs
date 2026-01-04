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
using MinecraftProtoNet.Baritone.Pathfinding.Precompute;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.State;
using static MinecraftProtoNet.Baritone.Pathfinding.Precompute.Ternary;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// Static helpers for cost calculation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java
/// </summary>
public static class MovementHelper
{
    // HORIZONTALS_BUT_ALSO_DOWN_____SO_EVERY_DIRECTION_EXCEPT_UP
    private static readonly BlockFace[] HorizontalsButAlsoDown = 
    {
        BlockFace.North, BlockFace.South, BlockFace.East, BlockFace.West, BlockFace.Bottom
    };

    public static bool AvoidBreaking(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        if (!bsi.WorldBorder.CanPlaceAt(x, z))
        {
            return true;
        }
        string blockName = state.Name;
        return Core.Baritone.Settings().BlocksToDisallowBreaking.Value.Contains(blockName)
                || blockName.Contains("ice", StringComparison.OrdinalIgnoreCase) // ice becomes water
                || blockName.Contains("infested", StringComparison.OrdinalIgnoreCase) // obvious reasons
                || AvoidAdjacentBreaking(bsi, x, y + 1, z, true)
                || AvoidAdjacentBreaking(bsi, x + 1, y, z, false)
                || AvoidAdjacentBreaking(bsi, x - 1, y, z, false)
                || AvoidAdjacentBreaking(bsi, x, y, z + 1, false)
                || AvoidAdjacentBreaking(bsi, x, y, z - 1, false);
    }

    public static bool AvoidAdjacentBreaking(BlockStateInterface bsi, int x, int y, int z, bool directlyAbove)
    {
        BlockState state = bsi.Get0(x, y, z);
        string blockName = state.Name;
        
        // Check for falling blocks
        if (!directlyAbove
            && (blockName.Contains("gravel", StringComparison.OrdinalIgnoreCase) 
                || blockName.Contains("sand", StringComparison.OrdinalIgnoreCase))
            && Core.Baritone.Settings().AvoidUpdatingFallingBlocks.Value
            && IsFree(bsi.Get0(x, y - 1, z)))
        {
            return true;
        }
        
        // Check for liquids
        if (state.IsLiquid)
        {
            if (directlyAbove || Core.Baritone.Settings().StrictLiquidCheck.Value)
            {
                return true;
            }
            // Check if it's a source block (level 0)
            if (state.Properties.TryGetValue("level", out var levelStr) && levelStr == "0")
            {
                return true; // source blocks like to flow horizontally
            }
            // Everything else will prefer flowing down
            return !bsi.Get0(x, y - 1, z).IsLiquid;
        }
        
        return state.IsLiquid;
    }

    public static bool CanWalkThrough(IPlayerContext ctx, BetterBlockPos pos)
    {
        return CanWalkThrough(new BlockStateInterface(ctx), pos.X, pos.Y, pos.Z);
    }

    public static bool CanWalkThrough(BlockStateInterface bsi, int x, int y, int z)
    {
        return CanWalkThrough(bsi, x, y, z, bsi.Get0(x, y, z));
    }

    public static bool CanWalkThrough(CalculationContext context, int x, int y, int z, BlockState state)
    {
        return context.PrecomputedData.CanWalkThrough(context.Bsi, x, y, z, state);
    }

    public static bool CanWalkThrough(CalculationContext context, int x, int y, int z)
    {
        return context.PrecomputedData.CanWalkThrough(context.Bsi, x, y, z, context.Get(x, y, z));
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

    public static Ternary CanWalkThroughBlockState(BlockState state)
    {
        string blockName = state.Name;
        if (state.IsAir)
        {
            return Yes;
        }
        // Blocks that definitely block movement
        if (blockName.Contains("fire", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("tripwire", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("cobweb", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("end_portal", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("cocoa", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("skull", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("bubble_column", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("shulker_box", StringComparison.OrdinalIgnoreCase)
            || state.IsSlab
            || blockName.Contains("trapdoor", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("honey_block", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("end_rod", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("sweet_berry_bush", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("pointed_dripstone", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("amethyst_cluster", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("azalea", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("big_dripleaf", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("powder_snow", StringComparison.OrdinalIgnoreCase))
        {
            return No;
        }
        if (Core.Baritone.Settings().BlocksToAvoid.Value.Contains(blockName))
        {
            return No;
        }
        // Doors and fence gates - assume openable (except iron door)
        if (blockName.Contains("door", StringComparison.OrdinalIgnoreCase) 
            || blockName.Contains("fence_gate", StringComparison.OrdinalIgnoreCase))
        {
            if (blockName.Contains("iron_door", StringComparison.OrdinalIgnoreCase))
            {
                return No;
            }
            return Yes;
        }
        if (blockName.Contains("carpet", StringComparison.OrdinalIgnoreCase))
        {
            return Maybe;
        }
        if (state.IsSnow)
        {
            return Maybe;
        }
        // Check fluid state
        if (state.IsLiquid)
        {
            // If not full fluid level, can't walk through
            if (state.Properties.TryGetValue("level", out var levelStr) && levelStr != "8")
            {
                return No;
            }
            return Maybe;
        }
        if (blockName.Contains("cauldron", StringComparison.OrdinalIgnoreCase))
        {
            return No;
        }
        // Use BlocksMotion as equivalent to isPathfindable(PathComputationType.LAND)
        return state.BlocksMotion ? No : Yes;
    }

    public static bool CanWalkThroughPosition(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        string blockName = state.Name;

        if (blockName.Contains("carpet", StringComparison.OrdinalIgnoreCase))
        {
            return CanWalkOn(bsi, x, y - 1, z);
        }

        if (state.IsSnow)
        {
            if (!bsi.WorldContainsLoadedChunk(x, z))
            {
                return true;
            }
            if (state.SnowLayers >= 3)
            {
                return false;
            }
            return CanWalkOn(bsi, x, y - 1, z);
        }

        if (state.IsLiquid)
        {
            if (IsFlowing(x, y, z, state, bsi))
            {
                return false;
            }
            if (Core.Baritone.Settings().AssumeWalkOnWater.Value)
            {
                return false;
            }

            BlockState up = bsi.Get0(x, y + 1, z);
            if (up.IsLiquid || blockName.Contains("lily_pad", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return blockName.Contains("water", StringComparison.OrdinalIgnoreCase);
        }

        return !state.BlocksMotion;
    }

    public static Ternary FullyPassableBlockState(BlockState state)
    {
        string blockName = state.Name;
        if (state.IsAir)
        {
            return Yes;
        }
        // Exceptions - blocks that are isPassable true, but we can't actually jump through
        if (blockName.Contains("fire", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("tripwire", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("cobweb", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("vine", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("ladder", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("cocoa", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("azalea", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("door", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("fence_gate", StringComparison.OrdinalIgnoreCase)
            || state.IsSnow
            || state.IsLiquid
            || blockName.Contains("trapdoor", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("end_portal", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("skull", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("shulker_box", StringComparison.OrdinalIgnoreCase))
        {
            return No;
        }
        return state.BlocksMotion ? No : Yes;
    }

    public static bool FullyPassable(CalculationContext context, int x, int y, int z)
    {
        return FullyPassable(context, x, y, z, context.Get(x, y, z));
    }

    public static bool FullyPassable(CalculationContext context, int x, int y, int z, BlockState state)
    {
        return context.PrecomputedData.FullyPassable(context.Bsi, x, y, z, state);
    }

    public static bool FullyPassablePosition(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        return !state.BlocksMotion;
    }

    public static bool IsReplaceable(int x, int y, int z, BlockState state, BlockStateInterface bsi)
    {
        string blockName = state.Name;
        if (state.IsAir)
        {
            return true;
        }
        if (state.IsSnow)
        {
            if (!bsi.WorldContainsLoadedChunk(x, z))
            {
                return true;
            }
            return state.SnowLayers == 1;
        }
        if (blockName.Contains("large_fern", StringComparison.OrdinalIgnoreCase) 
            || blockName.Contains("tall_grass", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:317
        // state.canBeReplaced() - for now use BlocksMotion as approximation
        // Blocks that can be replaced typically don't block motion
        return !state.BlocksMotion;
    }

    public static bool AvoidWalkingInto(BlockState state)
    {
        string blockName = state.Name;
        return state.IsLiquid
                || blockName.Contains("magma", StringComparison.OrdinalIgnoreCase)
                || blockName.Contains("cactus", StringComparison.OrdinalIgnoreCase)
                || blockName.Contains("sweet_berry_bush", StringComparison.OrdinalIgnoreCase)
                || blockName.Contains("fire", StringComparison.OrdinalIgnoreCase)
                || blockName.Contains("end_portal", StringComparison.OrdinalIgnoreCase)
                || blockName.Contains("cobweb", StringComparison.OrdinalIgnoreCase)
                || blockName.Contains("bubble_column", StringComparison.OrdinalIgnoreCase);
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

    public static Ternary CanWalkOnBlockState(BlockState state)
    {
        string blockName = state.Name;
        if (IsBlockNormalCube(state) && !blockName.Contains("magma", StringComparison.OrdinalIgnoreCase)
            && !blockName.Contains("bubble_column", StringComparison.OrdinalIgnoreCase)
            && !blockName.Contains("honey_block", StringComparison.OrdinalIgnoreCase))
        {
            return Yes;
        }
        if (blockName.Contains("azalea", StringComparison.OrdinalIgnoreCase))
        {
            return Yes;
        }
        if (blockName.Contains("ladder", StringComparison.OrdinalIgnoreCase)
            || (blockName.Contains("vine", StringComparison.OrdinalIgnoreCase) && Core.Baritone.Settings().AllowVines.Value))
        {
            return Yes;
        }
        if (blockName.Contains("farmland", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("dirt_path", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("soul_sand", StringComparison.OrdinalIgnoreCase))
        {
            return Yes;
        }
        if (blockName.Contains("ender_chest", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("chest", StringComparison.OrdinalIgnoreCase))
        {
            return Yes;
        }
        if (blockName.Contains("glass", StringComparison.OrdinalIgnoreCase))
        {
            return Yes;
        }
        if (state.IsStairs)
        {
            return Yes;
        }
        if (IsWater(state))
        {
            return Maybe;
        }
        if (IsLava(state) && Core.Baritone.Settings().AssumeWalkOnLava.Value)
        {
            return Maybe;
        }
        if (state.IsSlab)
        {
            if (!Core.Baritone.Settings().AllowWalkOnBottomSlab.Value)
            {
                if (!state.IsTop)
                {
                    return No;
                }
                return Yes;
            }
            return Yes;
        }
        return No;
    }

    public static bool CanWalkOnPosition(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        string blockName = state.Name;
        if (IsWater(state))
        {
            BlockState upState = bsi.Get0(x, y + 1, z);
            string upName = upState.Name;
            if (upName.Contains("lily_pad", StringComparison.OrdinalIgnoreCase) 
                || upName.Contains("carpet", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (IsFlowing(x, y, z, state, bsi) || upState.IsLiquid)
            {
                return IsWater(upState) && !Core.Baritone.Settings().AssumeWalkOnWater.Value;
            }
            return IsWater(upState) ^ Core.Baritone.Settings().AssumeWalkOnWater.Value;
        }

        if (IsLava(state) && !IsFlowing(x, y, z, state, bsi) && Core.Baritone.Settings().AssumeWalkOnLava.Value)
        {
            return true;
        }

        return false;
    }

    public static bool CanWalkOn(CalculationContext context, int x, int y, int z, BlockState state)
    {
        return context.PrecomputedData.CanWalkOn(context.Bsi, x, y, z, state);
    }

    public static bool CanWalkOn(CalculationContext context, int x, int y, int z)
    {
        return CanWalkOn(context, x, y, z, context.Get(x, y, z));
    }

    public static bool CanWalkOn(IPlayerContext ctx, BetterBlockPos pos, BlockState state)
    {
        return CanWalkOn(new BlockStateInterface(ctx), pos.X, pos.Y, pos.Z, state);
    }

    public static bool CanWalkOn(IPlayerContext ctx, BetterBlockPos pos)
    {
        return CanWalkOn(new BlockStateInterface(ctx), pos.X, pos.Y, pos.Z);
    }

    public static bool CanWalkOn(BlockStateInterface bsi, int x, int y, int z)
    {
        return CanWalkOn(bsi, x, y, z, bsi.Get0(x, y, z));
    }

    public static bool CanUseFrostWalker(CalculationContext context, BlockState state)
    {
        return context.FrostWalker != 0
                && IsWater(state)
                && state.Properties.TryGetValue("level", out var level) && level == "0";
    }

    public static bool MustBeSolidToWalkOn(CalculationContext context, int x, int y, int z, BlockState state)
    {
        string blockName = state.Name;
        if (blockName.Contains("ladder", StringComparison.OrdinalIgnoreCase) 
            || blockName.Contains("vine", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (state.IsLiquid)
        {
            if (state.IsSlab && state.IsTop)
            {
                return true;
            }
            if (state.IsStairs && state.Properties.TryGetValue("half", out var half) && half == "top")
            {
                return true;
            }
            if (blockName.Contains("trapdoor", StringComparison.OrdinalIgnoreCase)
                && state.Properties.TryGetValue("open", out var open) && open == "false"
                && state.Properties.TryGetValue("half", out var trapHalf) && trapHalf == "top")
            {
                return true;
            }
            if (blockName.Contains("scaffolding", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (blockName.Contains("leaves", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (context.AssumeWalkOnWater)
            {
                return false;
            }
            BlockState blockAbove = context.Get(x, y + 1, z);
            if (blockAbove.IsLiquid)
            {
                return false;
            }
        }
        return true;
    }

    public static bool CanPlaceAgainst(BlockStateInterface bsi, int x, int y, int z)
    {
        return CanPlaceAgainst(bsi, x, y, z, bsi.Get0(x, y, z));
    }

    public static bool CanPlaceAgainst(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        if (!bsi.WorldBorder.CanPlaceAt(x, z))
        {
            return false;
        }
        return IsBlockNormalCube(state) || state.Name.Contains("glass", StringComparison.OrdinalIgnoreCase);
    }

    public static double GetMiningDurationTicks(CalculationContext context, int x, int y, int z, bool includeFalling)
    {
        return GetMiningDurationTicks(context, x, y, z, context.Get(x, y, z), includeFalling);
    }

    public static double GetMiningDurationTicks(CalculationContext context, int x, int y, int z, BlockState state, bool includeFalling)
    {
        string blockName = state.Name;
        if (!CanWalkThrough(context, x, y, z, state))
        {
            if (state.IsLiquid)
            {
                return ActionCosts.CostInf;
            }
            double mult = context.BreakCostMultiplierAt(x, y, z, state);
            if (mult >= ActionCosts.CostInf)
            {
                return ActionCosts.CostInf;
            }
            if (AvoidBreaking(context.Bsi, x, y, z, state))
            {
                return ActionCosts.CostInf;
            }
            double strVsBlock = context.ToolSet.GetStrVsBlock(state);
            if (strVsBlock <= 0)
            {
                return ActionCosts.CostInf;
            }
            double result = 1 / strVsBlock;
            result += context.BreakBlockAdditionalCost;
            result *= mult;
            if (includeFalling)
            {
                BlockState above = context.Get(x, y + 1, z);
                string aboveName = above.Name;
                if (aboveName.Contains("gravel", StringComparison.OrdinalIgnoreCase) 
                    || aboveName.Contains("sand", StringComparison.OrdinalIgnoreCase))
                {
                    result += GetMiningDurationTicks(context, x, y + 1, z, above, true);
                }
            }
            return result;
        }
        return 0;
    }

    public static bool IsBottomSlab(BlockState state)
    {
        return state.IsSlab && !state.IsTop;
    }

    public static void SwitchToBestToolFor(IPlayerContext ctx, BlockState b)
    {
        SwitchToBestToolFor(ctx, b, new ToolSet(ctx.Player() as Entity), Core.Baritone.Settings().PreferSilkTouch.Value);
    }

    public static void SwitchToBestToolFor(IPlayerContext ctx, BlockState b, ToolSet ts, bool preferSilkTouch)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:646-650
        if (Core.Baritone.Settings().AutoTool.Value && !Core.Baritone.Settings().AssumeExternalAutoTool.Value)
        {
            var player = ctx.Player() as Entity;
            if (player != null)
            {
                int bestSlot = ts.GetBestSlot(b.Name, preferSilkTouch);
                player.HeldSlot = (short)bestSlot;
            }
        }
    }

    public static void MoveTowards(IPlayerContext ctx, MovementState state, BetterBlockPos pos)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:652-659
        var playerHead = ctx.PlayerHead();
        var playerRot = ctx.PlayerRotations();
        if (playerHead == null || playerRot == null) return;
        var rotation = RotationUtils.CalcRotationFromVec3d(
            playerHead,
            VecUtils.GetBlockPosCenter(pos),
            playerRot
        ).WithPitch(playerRot.GetPitch());
        
        state.SetTarget(new MovementState.MovementTarget(rotation, false))
            .SetInput(Input.MoveForward, true);
    }

    public static bool IsWater(BlockState state)
    {
        return state.Name.Contains("water", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLava(BlockState state)
    {
        return state.Name.Contains("lava", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLiquid(IPlayerContext ctx, BetterBlockPos p)
    {
        return IsLiquid(BlockStateInterface.Get(ctx, p));
    }

    public static bool IsLiquid(BlockState blockState)
    {
        return blockState.IsLiquid;
    }

    public static bool PossiblyFlowing(BlockState state)
    {
        if (!state.IsLiquid)
        {
            return false;
        }
        // Check if it's flowing (level != 8)
        return state.Properties.TryGetValue("level", out var level) && level != "8";
    }

    public static bool IsFlowing(int x, int y, int z, BlockState state, BlockStateInterface bsi)
    {
        if (!state.IsLiquid)
        {
            return false;
        }
        if (PossiblyFlowing(state))
        {
            return true;
        }
        return PossiblyFlowing(bsi.Get0(x + 1, y, z))
                || PossiblyFlowing(bsi.Get0(x - 1, y, z))
                || PossiblyFlowing(bsi.Get0(x, y, z + 1))
                || PossiblyFlowing(bsi.Get0(x, y, z - 1));
    }

    public static bool IsBlockNormalCube(BlockState state)
    {
        string blockName = state.Name;
        if (blockName.Contains("bamboo", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("piston", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("scaffolding", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("shulker_box", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("pointed_dripstone", StringComparison.OrdinalIgnoreCase)
            || blockName.Contains("amethyst_cluster", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        // Use BlocksMotion as equivalent to Block.isShapeFullBlock
        return state.BlocksMotion;
    }

    private static bool IsFree(BlockState state)
    {
        return state.IsAir || !state.BlocksMotion;
    }

    public enum PlaceResult
    {
        ReadyToPlace,
        Attempting,
        NoOption
    }

    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/MovementHelper.java:743-797
    public static PlaceResult AttemptToPlaceABlock(MovementState state, IBaritone baritone, BetterBlockPos pos, bool preferDown, bool wouldSneak)
    {
        var ctx = baritone.GetPlayerContext();
        var direct = RotationUtils.Reachable(ctx, pos, wouldSneak);
        bool found = false;
        
        if (direct != null)
        {
            state.SetTarget(new MovementState.MovementTarget(direct, true));
            found = true;
        }
        
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
            
            if (CanPlaceAgainst(new BlockStateInterface(ctx), against1.X, against1.Y, against1.Z))
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
                        // Check if adjacent position matches
                        var adjacentPos = result.GetAdjacentBlockPosition();
                        if (adjacentPos.X == pos.X && adjacentPos.Y == pos.Y && adjacentPos.Z == pos.Z)
                        {
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
        
        // Check if already looking at the block
        var selectedBlock = ctx.GetSelectedBlock();
        if (selectedBlock != null)
        {
            if (selectedBlock.X == pos.X && selectedBlock.Y == pos.Y && selectedBlock.Z == pos.Z)
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

    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/MovementHelper.java
    // Checks if a block (represented as object/string) is transparent
    public static bool IsTransparent(object block)
    {
        // For now, treat block names as strings and check if they're air or transparent blocks
        if (block is string blockName)
        {
            return blockName.Contains("air", StringComparison.OrdinalIgnoreCase) ||
                   blockName.Contains("glass", StringComparison.OrdinalIgnoreCase) ||
                   blockName.Contains("slab", StringComparison.OrdinalIgnoreCase) ||
                   blockName.Contains("stairs", StringComparison.OrdinalIgnoreCase) ||
                   blockName.Contains("fence", StringComparison.OrdinalIgnoreCase) ||
                   blockName.Contains("wall", StringComparison.OrdinalIgnoreCase);
        }
        // If it's not a string, assume it's not transparent (conservative approach)
        return false;
    }
}

