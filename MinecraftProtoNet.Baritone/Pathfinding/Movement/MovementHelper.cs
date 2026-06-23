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
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.State.Base;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// Helper methods for movement execution.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java
/// </summary>
public static class MovementHelper
{
    private static readonly ILogger _logger = LoggingConfiguration.CreateLogger("MovementHelper");
    public static readonly BlockFace[] HorizontalsButAlsoDown = 
    {
        BlockFace.North, BlockFace.South, BlockFace.East, BlockFace.West, BlockFace.Bottom
    };

    /// <summary>
    /// Sets the movement target rotation and forward input to move towards a destination block.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:652-658
    /// NOTE: This method intentionally does NOT set sprint. Sprint must be controlled by individual
    /// movement UpdateState methods, as each movement type has different sprint safety conditions.
    /// </summary>
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

    /// <summary>
    /// Calculates the mining duration in ticks for a block at the given position.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:589-622
    /// </summary>
    public static double GetMiningDurationTicks(CalculationContext context, int x, int y, int z, BlockState state, bool includeFalling)
    {
        if (state == null) return 0;
        
        // Reference: MovementHelper.java:595 - Only compute mining cost for blocks we CANT walk through
        if (!CanWalkThrough(context, x, y, z, state))
        {
            // Reference: MovementHelper.java:596-598 - Can't mine fluids
            if (state.IsLiquid)
            {
                return ActionCosts.CostInf;
            }
            
            // Reference: MovementHelper.java:599-601 - Break cost multiplier (handles AllowBreak and protection)
            double mult = context.BreakCostMultiplierAt(x, y, z, state);
            if (mult >= ActionCosts.CostInf)
            {
                return ActionCosts.CostInf;
            }
            
            // Reference: MovementHelper.java:603-605 - Avoid breaking certain blocks
            if (AvoidBreaking(context.Bsi, x, y, z, state))
            {
                return ActionCosts.CostInf;
            }
            
            // Reference: MovementHelper.java:606-610
            // getStrVsBlock returns 1/(time in ticks), so 1/strVsBlock = time in ticks
            double strVsBlock = context.ToolSet.GetStrVsBlock(state);
            if (strVsBlock <= 0)
            {
                return ActionCosts.CostInf;
            }
            
            double result = 1.0 / strVsBlock;
            result += context.BreakBlockAdditionalCost;
            result *= mult;
            
            // Reference: MovementHelper.java:613-617 - Add cost for falling blocks above
            if (includeFalling)
            {
                var above = context.Get(x, y + 1, z);
                if (above != null && above.Name.Contains("sand", StringComparison.OrdinalIgnoreCase) 
                    || (above != null && above.Name.Contains("gravel", StringComparison.OrdinalIgnoreCase)))
                {
                    result += GetMiningDurationTicks(context, x, y + 1, z, above, true);
                }
            }
            
            return result;
        }
        
        return 0; // We won't actually mine it, so don't check fallings above
    }

    // Reference: MovementHelper.java isWater - getFluidState().getType() == WATER/FLOWING_WATER (includes waterlogged)
    public static bool IsWater(BlockState state)
    {
        return state != null && state.IsWater;
    }

    // Reference: MovementHelper.java isLava - getFluidState().getType() == LAVA/FLOWING_LAVA
    public static bool IsLava(BlockState state)
    {
        return state != null && state.IsLava;
    }

    // Reference: MovementHelper.java isLiquid(BlockState) - !getFluidState().isEmpty() (any fluid, incl. waterlogged)
    public static bool IsLiquid(BlockState state)
    {
        return state != null && (state.IsWater || state.IsLava);
    }

    public static bool IsLiquid(IPlayerContext ctx, BetterBlockPos pos)
    {
        var state = BlockStateInterface.Get(ctx, pos);
        return IsLiquid(state);
    }

    // Reference: MovementHelper.java isClimbable(Block) - LADDER, VINE, WEEPING_VINES(_PLANT), TWISTING_VINES(_PLANT)
    public static bool IsClimbable(BlockState state)
    {
        return state != null && state.IsClimbable;
    }

    // Reference: MovementHelper.java isBottomSlab - instanceof SlabBlock && TYPE == BOTTOM
    public static bool IsBottomSlab(BlockState state)
    {
        if (state == null) return false;
        return state.IsSlab && state.Properties.TryGetValue("type", out var type) && type == "bottom";
    }

    // Reference: MovementHelper.java:772-788 - exclude a few classes whose collision shape is (or can be)
    // a full cube but which must not count as a normal cube, then test the real collision shape.
    public static bool IsBlockNormalCube(BlockState state)
    {
        if (state == null) return false;
        var name = state.Name;
        if (name.Equals("minecraft:bamboo", StringComparison.OrdinalIgnoreCase)         // BambooStalkBlock
            || name.Equals("minecraft:moving_piston", StringComparison.OrdinalIgnoreCase) // MovingPistonBlock
            || name.Equals("minecraft:scaffolding", StringComparison.OrdinalIgnoreCase)  // ScaffoldingBlock
            || name.EndsWith("shulker_box", StringComparison.OrdinalIgnoreCase)          // ShulkerBoxBlock (full collision box!)
            || name.Equals("minecraft:pointed_dripstone", StringComparison.OrdinalIgnoreCase) // PointedDripstoneBlock
            || name.EndsWith("amethyst_cluster", StringComparison.OrdinalIgnoreCase)     // AmethystClusterBlock
            || name.EndsWith("amethyst_bud", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        // Block.isShapeFullBlock(getCollisionShape()) via the shared shape registry.
        return BlockShapeRegistry.Shared.GetShape(state).IsFullCube();
    }

    /// <summary>
    /// Checks if a block can be replaced when placing another block.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:291-318
    /// Maps to Minecraft's state.canBeReplaced() plus special cases for snow layers.
    /// </summary>
    public static bool IsReplaceable(int x, int y, int z, BlockState state, BlockStateInterface bsi)
    {
        if (state == null) return true;
        
        // Air is always replaceable
        if (state.IsAir) return true;
        
        // Reference: MovementHelper.java:307-313 - Snow layers special case
        if (state.IsSnow)
        {
            if (!bsi.WorldContainsLoadedChunk(x, z))
            {
                return true; // Default to true for unloaded chunks
            }
            return state.SnowLayers == 1;
        }
        
        // Reference: MovementHelper.java:314-315 - Large fern and tall grass
        string name = state.Name;
        if (name.Equals("minecraft:large_fern", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:tall_grass", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        // Reference: Minecraft's BlockBehaviour state.canBeReplaced() - blocks that return true
        // This covers: dead_bush, short_grass, fern, vine, seagrass, tall_seagrass,
        // glow_lichen, fire, soul_fire, water, lava, structure_void, light, etc.
        if (name.Equals("minecraft:dead_bush", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:short_grass", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:fern", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:vine", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:seagrass", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:tall_seagrass", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:glow_lichen", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:fire", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:soul_fire", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:structure_void", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:light", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:warped_roots", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:crimson_roots", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("minecraft:nether_sprouts", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        // Liquids are replaceable
        if (IsWater(state) || IsLava(state)) return true;
        
        // Grass block name contains "grass" but is NOT replaceable; short_grass/tall_grass are handled above
        // So we do NOT use a broad name.Contains("grass") check here
        
        return false;
    }

    public static bool CanPlaceAgainst(BlockStateInterface bsi, int x, int y, int z)
    {
        return CanPlaceAgainst(bsi, x, y, z, bsi.Get0(x, y, z));
    }

    // Reference: MovementHelper.java:575-582 - world border + a face we can actually place against.
    // Only normal cubes and (stained) glass — deliberately NOT fences/slabs/panes/carpet etc.
    public static bool CanPlaceAgainst(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        if (!bsi.WorldBorder.CanPlaceAt(x, z))
        {
            return false;
        }
        if (state == null)
        {
            return false;
        }
        return IsBlockNormalCube(state)
            || state.Name.Equals("minecraft:glass", StringComparison.OrdinalIgnoreCase)
            || state.Name.EndsWith("_stained_glass", StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// Switches to the best tool for breaking the given block.
    /// First checks hotbar (like vanilla Baritone), then scans main inventory
    /// and swaps the best tool to the current hotbar slot if needed.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java
    /// </summary>
    public static void SwitchToBestToolFor(IPlayerContext ctx, BlockState state)
    {
        var player = ctx.Player() as Entity;
        if (player == null) return;

        var toolSet = new ToolSet(player);
        int bestHotbarSlot = toolSet.GetBestSlot(state.Name, false);

        // Check if the hotbar tool is actually effective
        int bestContainerSlot = bestHotbarSlot + 36;
        var bestItem = player.Inventory.GetSlot((short)bestContainerSlot);
        double hotbarSpeed = ToolSet.CalculateSpeedVsBlock(bestItem, state);

        // Scan main inventory (slots 9-35) for a better tool
        // This extends vanilla Baritone behavior to handle tools not yet in hotbar
        int bestInvSlot = -1;
        double bestInvSpeed = hotbarSpeed;

        for (int i = 9; i <= 35; i++)
        {
            var item = player.Inventory.GetSlot((short)i);
            if (item.ItemId == null || item.ItemCount <= 0) continue;

            double speed = ToolSet.CalculateSpeedVsBlock(item, state);
            if (speed > bestInvSpeed)
            {
                bestInvSpeed = speed;
                bestInvSlot = i;
            }
        }

        // Resolve item names for logging
        string? hotbarItemName = null;
        if (bestItem.ItemId.HasValue)
            ClientState.ItemRegistry?.TryGetValue(bestItem.ItemId.Value, out hotbarItemName);

        if (bestInvSlot >= 0)
        {
            // Found a better tool in main inventory — swap it to the current hotbar slot
            // using a number-key swap (Mode 2). Fire-and-forget; the packet sender is async
            // but EnsureHasSentCarriedItemAsync on the next tick will sync held slot.
            int targetHotbarSlot = player.HeldSlot;
            int targetContainerSlot = targetHotbarSlot + 36;

            // Swap inventory slot with hotbar slot locally
            var invItem = player.Inventory.GetSlot((short)bestInvSlot);
            var hotbarItem = player.Inventory.GetSlot((short)targetContainerSlot);
            player.Inventory.SetSlot((short)bestInvSlot, hotbarItem);
            player.Inventory.SetSlot((short)targetContainerSlot, invItem);

            string? invItemName = null;
            if (invItem.ItemId.HasValue)
                ClientState.ItemRegistry?.TryGetValue(invItem.ItemId.Value, out invItemName);
            _logger.LogInformation("[ToolSwitch] Block={Block}, Swapping {InvTool} (inv slot {InvSlot}) to hotbar slot {HotbarSlot} (speed {Speed:F3} > hotbar best {HotbarSpeed:F3} with {HotbarTool})",
                state.Name, invItemName ?? "unknown", bestInvSlot, targetHotbarSlot, bestInvSpeed, hotbarSpeed, hotbarItemName ?? "empty");

            // Send the swap packet (fire-and-forget from sync context)
            var client = (IPacketSender)ctx.Minecraft();
            _ = client.SendPacketAsync(new ContainerClickPacket
            {
                WindowId = 0,
                StateId = player.Inventory.StateId,
                Slot = (short)bestInvSlot,
                Button = (sbyte)targetHotbarSlot,
                Mode = ClickContainerMode.Swap,
                ChangedSlots = new Dictionary<short, Slot>
                {
                    [(short)bestInvSlot] = hotbarItem,
                    [(short)targetContainerSlot] = invItem
                },
                CarriedItem = player.Inventory.CursorItem
            });

            // Keep held slot the same — tool is now there
        }
        else
        {
            // Best tool is already in hotbar — just switch to it
            if (bestHotbarSlot != player.HeldSlot)
            {
                _logger.LogInformation("[ToolSwitch] Block={Block}, Switching to hotbar slot {Slot} ({Tool}, speed {Speed:F3})",
                    state.Name, bestHotbarSlot, hotbarItemName ?? "empty hand", hotbarSpeed);
            }
            player.HeldSlot = (short)bestHotbarSlot;
        }
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

    // Reference: MovementHelper.java:857-867 - the floor blocks under the player's bounding-box footprint
    // (used to decide whether to sneak when any supporting block is magma).
    public static List<BetterBlockPos> SteppingOnBlocks(IPlayerContext ctx)
    {
        var blocks = new List<BetterBlockPos>();
        var player = ctx.Player() as Entity;
        if (player == null) return blocks;
        var bb = player.GetBoundingBox();
        var bp = player.BlockPosition();
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                // unit cube at the player's feet-Y level, offset (x,z); if the AABB overlaps it, the block below is stood on
                if (bb.Intersects(bp.X + x, bp.Y, bp.Z + z, bp.X + x + 1, bp.Y + 1, bp.Z + z + 1))
                {
                    blocks.Add(new BetterBlockPos(bp.X + x, bp.Y - 1, bp.Z + z));
                }
            }
        }
        return blocks;
    }

    // Reference: MovementHelper.java:514-519 - canUseFrostWalker(ctx, pos) reads the live FrostWalker enchant.
    public static bool CanUseFrostWalker(IPlayerContext ctx, BetterBlockPos pos)
    {
        var player = ctx.Player() as Entity;
        if (player == null) return false;
        if (global::MinecraftProtoNet.Core.Data.EnchantmentHelper.GetArmorEnchantmentLevel("minecraft:frost_walker", player) <= 0) return false;
        var state = BlockStateInterface.Get(ctx, pos);
        return state != null && IsWater(state) &&
               state.Properties.TryGetValue("level", out var level) && level == "0";
    }

    // Reference: MovementHelper.java:67-81
    public static bool AvoidBreaking(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        if (!bsi.WorldBorder.CanPlaceAt(x, z))
        {
            return true;
        }
        // NOTE: blocksToDisallowBreaking (NOT blocksToAvoidBreaking — that is ToolSet's cost-mult setting).
        return BaritoneAPI.GetSettings().BlocksToDisallowBreaking.Value.Contains(state.Name)
            || state.Name.Equals("minecraft:ice", StringComparison.OrdinalIgnoreCase) // ice becomes water, and water can mess up the path
            || state.Name.Contains("infested", StringComparison.OrdinalIgnoreCase) // InfestedBlock — obvious reasons
            || AvoidAdjacentBreaking(bsi, x, y + 1, z, true)
            || AvoidAdjacentBreaking(bsi, x + 1, y, z, false)
            || AvoidAdjacentBreaking(bsi, x - 1, y, z, false)
            || AvoidAdjacentBreaking(bsi, x, y, z + 1, false)
            || AvoidAdjacentBreaking(bsi, x, y, z - 1, false);
    }

    public static bool AvoidBreaking(CalculationContext context, int x, int y, int z, BlockState state)
    {
        return AvoidBreaking(context.Bsi, x, y, z, state);
    }

    // Reference: MovementHelper.java:83-110
    public static bool AvoidAdjacentBreaking(BlockStateInterface bsi, int x, int y, int z, bool directlyAbove)
    {
        // returns true if you should avoid breaking a block adjacent to this one (e.g. lava that will start flowing).
        // called for north/south/east/west/up only — NOT down. We assume breaking the block ABOVE liquid is always okay.
        var state = bsi.Get0(x, y, z);
        if (!directlyAbove // fine to mine a block that has a falling block directly above (that cost is included elsewhere)
            && state.IsFallingBlock
            && BaritoneAPI.GetSettings().AvoidUpdatingFallingBlocks.Value
            && FallingBlockIsFree(bsi, x, y - 1, z)) // and it would fall (unsupported)
        {
            return true; // don't break next to unsupported gravel — causes weird stuff
        }
        // only pure liquids for now; waterlogged blocks can have closed bottom sides
        if (state.IsLiquid)
        {
            if (directlyAbove || BaritoneAPI.GetSettings().StrictLiquidCheck.Value)
            {
                return true;
            }
            int level = state.Properties.TryGetValue("level", out var lv) && int.TryParse(lv, out var parsed) ? parsed : 0;
            if (level == 0)
            {
                return true; // source blocks like to flow horizontally
            }
            // everything else prefers flowing down
            return !bsi.Get0(x, y - 1, z).IsLiquid; // assume everything is in a static state
        }
        return IsLiquid(state); // !getFluidState().isEmpty() — waterlogged etc.
    }

    // Reference: minecraft FallingBlock.isFree — air / fire / liquid / replaceable (approximated via Baritone IsReplaceable)
    private static bool FallingBlockIsFree(BlockStateInterface bsi, int x, int y, int z)
    {
        var state = bsi.Get0(x, y, z);
        return state.IsAir
            || IsLiquid(state)
            || ClientState.BlockTags.HasTag(state.Name, "fire")
            || IsReplaceable(x, y, z, state, bsi);
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

    // Reference: MovementHelper.java:334-345
    public static bool IsDoorPassable(IPlayerContext ctx, BetterBlockPos doorPos, BetterBlockPos playerPos)
    {
        if (playerPos.Equals(doorPos))
        {
            return false;
        }
        var state = BlockStateInterface.Get(ctx, doorPos);
        if (state == null || !state.IsDoor)
        {
            return true;
        }
        return IsHorizontalBlockPassable(doorPos, state, playerPos);
    }

    // Reference: MovementHelper.java:347-358 - a gate only blocks when closed (facing-independent).
    public static bool IsGatePassable(IPlayerContext ctx, BetterBlockPos gatePos, BetterBlockPos playerPos)
    {
        if (playerPos.Equals(gatePos))
        {
            return false;
        }
        var state = BlockStateInterface.Get(ctx, gatePos);
        if (state == null || !state.IsFenceGate)
        {
            return true;
        }
        return state.Properties.TryGetValue("open", out var open) && open == "true";
    }

    // Reference: MovementHelper.java:360-378 - passable iff (facing axis == approach axis) == open.
    private static bool IsHorizontalBlockPassable(BetterBlockPos blockPos, BlockState state, BetterBlockPos playerPos)
    {
        if (playerPos.Equals(blockPos))
        {
            return false;
        }
        if (!state.Properties.TryGetValue("facing", out var facing))
        {
            return true;
        }
        char facingAxis = facing is "north" or "south" ? 'z' : 'x';
        bool open = state.Properties.TryGetValue("open", out var o) && o == "true";

        char playerFacing;
        if (playerPos.North().Equals(blockPos) || playerPos.South().Equals(blockPos))
        {
            playerFacing = 'z';
        }
        else if (playerPos.East().Equals(blockPos) || playerPos.West().Equals(blockPos))
        {
            playerFacing = 'x';
        }
        else
        {
            return true;
        }

        return (facingAxis == playerFacing) == open;
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

    // Block-class identity sets backed by the data report's definition.type (real Java block classes, no substrings).
    private static readonly HashSet<string> SkullTypes = new() // AbstractSkullBlock (NOT piston_head)
    {
        "minecraft:skull", "minecraft:wall_skull", "minecraft:wither_skull", "minecraft:wither_wall_skull",
        "minecraft:player_head", "minecraft:player_wall_head", "minecraft:piglinwallskull",
    };
    private static readonly HashSet<string> CauldronTypes = new() // CauldronBlock
    {
        "minecraft:cauldron", "minecraft:layered_cauldron", "minecraft:lava_cauldron",
    };
    private static bool IsSkull(BlockState s) => s.BlockType != null && SkullTypes.Contains(s.BlockType);
    private static bool IsCauldron(BlockState s) => s.BlockType != null && CauldronTypes.Contains(s.BlockType);

    // Reference: MovementHelper.java:417-456
    public static Ternary CanWalkOnBlockState(BlockState state)
    {
        if (state == null) return No;
        var settings = BaritoneAPI.GetSettings();
        var bt = state.BlockType;
        if (IsBlockNormalCube(state)
            && (!state.IsMagmaBlock || settings.AllowWalkOnMagmaBlocks.Value)
            && bt != "minecraft:bubble_column"
            && bt != "minecraft:honey")
        {
            return Yes;
        }
        if (bt == "minecraft:azalea") return Yes;
        if (state.IsLadder || (IsClimbable(state) && settings.AllowVines.Value)) return Yes;
        if (bt == "minecraft:farmland" || bt == "minecraft:dirt_path" || state.IsSoulSand) return Yes;
        if (bt == "minecraft:ender_chest" || bt == "minecraft:chest" || bt == "minecraft:trapped_chest") return Yes;
        if (bt == "minecraft:transparent" || bt == "minecraft:stained_glass") return Yes; // glass + stained glass
        if (state.IsStairs) return Yes;
        if (IsWater(state)) return Maybe;
        if (IsLava(state) && settings.AssumeWalkOnLava.Value) return Maybe;
        if (state.IsSlab)
        {
            if (!settings.AllowWalkOnBottomSlab.Value)
            {
                return state.Properties.GetValueOrDefault("type", "bottom") != "bottom" ? Yes : No;
            }
            return Yes;
        }
        return No;
    }

    // Reference: MovementHelper.java:139-192
    public static Ternary CanWalkThroughBlockState(BlockState state)
    {
        if (state == null) return Yes;
        if (state.IsAir) return Yes;
        var settings = BaritoneAPI.GetSettings();
        var bt = state.BlockType;
        if (bt == "minecraft:fire" || bt == "minecraft:soul_fire" || bt == "minecraft:web"
            || bt == "minecraft:end_portal" || bt == "minecraft:cocoa" || IsSkull(state)
            || bt == "minecraft:bubble_column" || bt == "minecraft:shulker_box" || state.IsSlab
            || bt == "minecraft:trapdoor" || bt == "minecraft:honey" || bt == "minecraft:end_rod"
            || bt == "minecraft:sweet_berry_bush" || bt == "minecraft:pointed_dripstone"
            || bt == "minecraft:amethyst_cluster" || bt == "minecraft:azalea")
        {
            return No;
        }
        if (bt == "minecraft:big_dripleaf") return No;
        if (bt == "minecraft:powder_snow") return No;
        if (settings.BlocksToAvoid.Value.Contains(state.Name)) return No;
        if (bt == "minecraft:door" || state.IsFenceGate)
        {
            // TODO: assumes all modded doors are openable
            if (state.Name.Equals("minecraft:iron_door", StringComparison.OrdinalIgnoreCase)) return No;
            return Yes;
        }
        if (state.IsCarpet) return Maybe;
        if (state.IsSnow) return Maybe; // unknown depth when cached as a top block
        if (IsLiquid(state))
        {
            // amount != 8 (flowing) → NO; source/full (amount 8) → MAYBE
            bool source = !state.Properties.TryGetValue("level", out var lv) || !int.TryParse(lv, out var lvl) || lvl == 0;
            return source ? Maybe : No;
        }
        if (IsCauldron(state)) return No;
        // Default: isPathfindable(LAND) ~= the block doesn't block motion.
        return !state.BlocksMotion ? Yes : No;
    }

    // Reference: MovementHelper.java:239-280
    public static Ternary FullyPassableBlockState(BlockState state)
    {
        if (state == null) return Yes;
        if (state.IsAir) return Yes;
        var bt = state.BlockType;
        // blocks that are passable but we can't actually jump through
        if (bt == "minecraft:fire" || bt == "minecraft:soul_fire" || bt == "minecraft:tripwire"
            || bt == "minecraft:web" || bt == "minecraft:vine" || bt == "minecraft:ladder"
            || bt == "minecraft:cocoa" || bt == "minecraft:azalea" || bt == "minecraft:door"
            || state.IsFenceGate || state.IsSnow || IsLiquid(state) || bt == "minecraft:trapdoor"
            || bt == "minecraft:end_portal" || IsSkull(state) || bt == "minecraft:shulker_box")
        {
            return No;
        }
        return !state.BlocksMotion ? Yes : No;
    }

    // Reference: MovementHelper.java:458-482
    public static bool CanWalkOnPosition(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        var settings = BaritoneAPI.GetSettings();
        if (IsWater(state))
        {
            var upState = bsi.Get0(x, y + 1, z);
            if (upState.IsLilyPad || upState.IsCarpet) return true;
            if (IsFlowing(x, y, z, state, bsi) || (IsWater(upState) && IsFlowing(x, y + 1, z, upState, bsi)))
            {
                // only walk on flowing water if it's under still water with jesus off
                return IsWater(upState) && !settings.AssumeWalkOnWater.Value;
            }
            // jesus on → walk on water only if no water above; jesus off → only if water above
            return IsWater(upState) ^ settings.AssumeWalkOnWater.Value;
        }
        if (IsLava(state) && !IsFlowing(x, y, z, state, bsi) && settings.AssumeWalkOnLava.Value)
        {
            return true;
        }
        return false;
    }

    // Reference: MovementHelper.java:194-237
    public static bool CanWalkThroughPosition(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        if (state.IsCarpet)
        {
            return CanWalkOn(bsi, x, y - 1, z);
        }
        if (state.IsSnow)
        {
            if (!bsi.WorldContainsLoadedChunk(x, z)) return true;
            if (state.SnowLayers >= 3) return false; // 3+ is impassable in a 2-high gap
            return CanWalkOn(bsi, x, y - 1, z);
        }
        if (IsLiquid(state))
        {
            if (IsFlowing(x, y, z, state, bsi)) return false;
            if (BaritoneAPI.GetSettings().AssumeWalkOnWater.Value) return false;
            var up = bsi.Get0(x, y + 1, z);
            if (IsLiquid(up) || up.IsLilyPad) return false;
            return state.IsWater;
        }
        return !state.BlocksMotion; // isPathfindable(LAND)
    }

    public static bool FullyPassablePosition(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        return !state.BlocksMotion;
    }

    public enum PlaceResult
    {
        ReadyToPlace,
        Attempting,
        NoOption
    }

    /// <summary>
    /// Attempts to place a block at the given position by finding a valid adjacent block to place against.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:743-797
    /// </summary>
    public static PlaceResult AttemptToPlaceABlock(MovementState state, IBaritone baritone, BetterBlockPos pos, bool preferDown, bool wouldSneak)
    {
        var ctx = baritone.GetPlayerContext();
        // Reference: MovementHelper.java:745-750 - Direct reachability check
        var direct = RotationUtils.Reachable(ctx, pos, wouldSneak);
        bool found = false;
        if (direct != null)
        {
            state.SetTarget(new MovementState.MovementTarget(direct, true));
            found = true;
        }
        
        var bsi = new BlockStateInterface(ctx);
        
        // Try placing against adjacent blocks
        // Reference: MovementHelper.java:751-776
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
                
                // Face center between pos and against1. For vertical faces (same Y) use block vertical center
                // so the ray from above doesn't hit the top of the block first (causing NoOption when bridging).
                double faceX = (pos.X + against1.X + 1.0) * 0.5;
                double faceY = (pos.Y == against1.Y)
                    ? pos.Y + 0.5  // Vertical face: use block center so we hit the side face, not the top
                    : (pos.Y + against1.Y + 0.5) * 0.5;
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
        
        // Check if already looking at the block
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
                
                // CRITICAL: Even if we are already looking, we must SET THE TARGET to ensure it persists 
                // and isn't clobbered by the previous movement's rotation or other logic.
                var playerRot = ctx.PlayerRotations();
                if (playerRot != null)
                {
                    state.SetTarget(new MovementState.MovementTarget(playerRot, true));
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
