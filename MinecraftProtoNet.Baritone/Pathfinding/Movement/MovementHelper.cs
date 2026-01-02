using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Pathfinding;
using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// Helper utilities for block classification and movement feasibility.
/// Based on Baritone's MovementHelper.java.
/// </summary>
public static class MovementHelper
{
    // ===== Block Passability Checks =====

    /// <summary>
    /// Returns whether a player can walk through this block (no collision).
    /// </summary>
    public static bool CanWalkThrough(BlockState? block)
    {
        if (block == null) return false;
        if (block.IsAir) return true;
        
        // Baritone MovementHelper.canWalkThroughBlockState
        var name = block.Name.ToLowerInvariant();

        // Blocks that strictly block motion for pathfinding (Baritone 1:1)
        if (name.Contains("cobweb") || name.Contains("portal") || 
            name.Contains("cocoa") || name.Contains("skull") || name.Contains("shulker_box") ||
            name.Contains("slab") || name.Contains("trapdoor") || name.Contains("honey_block") ||
            name.Contains("end_rod") || name.Contains("berry_bush") || name.Contains("dripstone") ||
            name.Contains("amethyst") || name.Contains("azalea") || name.Contains("dripleaf") ||
            name.Contains("cauldron") || name.Contains("big_dripleaf") || name.Contains("powder_snow") ||
            name.Contains("fire") || name.Contains("cactus"))
        {
            return false;
        }

        // Doors and Fence Gates (Openable)
        if (name.Contains("door") || name.Contains("fence_gate"))
        {
            if (name.Contains("iron_door")) return false;
            return true; // Assume openable for cost calculation
        }

        // Liquids (Handle separately for swimming vs walking)
        if (block.IsLiquid) return false; 

        return !block.BlocksMotion;
    }

    /// <summary>
    /// Returns whether a player can walk through the block at this position.
    /// </summary>
    public static bool CanWalkThrough(CalculationContext context, int x, int y, int z)
    {
        var block = context.GetBlockState(x, y, z);
        return CanWalkThrough(block);
    }

    /// <summary>
    /// Returns whether a player can stand on this block.
    /// </summary>
    public static bool CanWalkOn(BlockState? block)
    {
        if (block == null) return false;
        
        // Baritone MovementHelper.canWalkOnBlockState
        if (block.BlocksMotion && !IsMagmaBlock(block) && !IsHoneyBlock(block))
        {
            return true;
        }

        var name = block.Name.ToLowerInvariant();
        if (name.Contains("azalea") || name.Contains("ladder") || name.Contains("vine") ||
            name.Contains("farmland") || name.Contains("dirt_path") || name.Contains("soul_sand") ||
            name.Contains("chest") || name.Contains("glass") || name.Contains("stair") ||
            name.Contains("slab"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether a player can stand on the block at this position.
    /// </summary>
    public static bool CanWalkOn(CalculationContext context, int x, int y, int z)
    {
        var block = context.GetBlockState(x, y, z);
        return CanWalkOn(block);
    }

    // ===== Liquid Checks =====

    /// <summary>
    /// Returns whether this block is water.
    /// </summary>
    public static bool IsWater(BlockState? block) => block?.IsLiquid == true && block.Name.Contains("water", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether this block is lava.
    /// </summary>
    public static bool IsLava(BlockState? block) => block?.IsLiquid == true && block.Name.Contains("lava", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether this block is any liquid (water or lava).
    /// </summary>
    public static bool IsLiquid(BlockState? block)
    {
        if (block == null) return false;
        return block.IsLiquid;
    }

    /// <summary>
    /// Returns whether this block is air.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java
    /// </summary>
    public static bool IsAir(BlockState? block)
    {
        if (block == null) return false;
        return block.IsAir;
    }

    /// <summary>
    /// Returns whether this block is a falling block (sand, gravel, concrete powder, etc.).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java
    /// </summary>
    public static bool IsFallingBlock(BlockState? block)
    {
        if (block == null) return false;
        var name = block.Name.ToLowerInvariant();
        return name.Contains("sand") || name.Contains("gravel") || 
               name.Contains("concrete_powder") || name.Contains("anvil");
    }

    // ===== Special Block Checks =====

    /// <summary>
    /// Returns whether this block is a ladder.
    /// </summary>
    public static bool IsLadder(BlockState? block) => block?.Name.Contains("ladder", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Returns whether this block is a vine.
    /// </summary>
    public static bool IsVine(BlockState? block) => block?.Name.Contains("vine", StringComparison.OrdinalIgnoreCase) == true && !block.Name.Contains("vines", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether this block is climbable (ladder or vine).
    /// </summary>
    public static bool IsClimbable(BlockState? block)
    {
        return IsLadder(block) || IsVine(block);
    }

    /// <summary>
    /// Returns whether this block is a door.
    /// </summary>
    public static bool IsDoor(BlockState? block)
    {
        if (block == null) return false;
        return block.Name.Contains("door", StringComparison.OrdinalIgnoreCase) &&
               !block.Name.Contains("trapdoor", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns whether this block is a fence gate.
    /// </summary>
    public static bool IsFenceGate(BlockState? block)
    {
        if (block == null) return false;
        return block.Name.Contains("fence_gate", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns whether we should avoid walking into this block.
    /// This represents blocks that cause damage or significant slowing.
    /// </summary>
    public static bool AvoidWalkingInto(BlockState? block)
    {
        if (block == null) return false;
        
        // Baritone MovementHelper.avoidWalkingInto
        // Note: Liquids are handled as "avoid" in Baritone if Jesus/WaterBucket is off.
        if (block.IsLiquid) return true;
        
        var name = block.Name.ToLowerInvariant();
        return name.Contains("magma") || name.Contains("cactus") || name.Contains("berry_bush") ||
               name.Contains("fire") || name.Contains("portal") || name.Contains("cobweb") ||
               name.Contains("bubble_column");
    }


    /// <summary>
    /// Returns whether this block is replaceable (can be placed into).
    /// </summary>
    public static bool IsReplaceable(BlockState? block)
    {
        if (block == null) return false;
        if (block.IsAir || block.IsLiquid) return true;

        var name = block.Name.ToLowerInvariant();
        return name.Contains("grass") || name.Contains("flower") || name.Contains("snow_layer") ||
               name.Contains("dead_bush") || name.Contains("fern") || name.Contains("tall_grass");
    }

    // ===== Special Floor Checks =====

    /// <summary>
    /// Returns whether we must be standing on something solid to be at this position.
    /// (i.e., the player isn't swimming or floating)
    /// </summary>
    public static bool MustBeSolidToWalkOn(CalculationContext context, int x, int y, int z)
    {
        var block = context.GetBlockState(x, y, z);
        if (block == null) return false;

        // If there's water, we can float
        if (IsWater(block)) return false;

        // If there's lava, we definitely need solid ground
        if (IsLava(block)) return true;

        // Check the floor block
        var floor = context.GetBlockState(x, y - 1, z);
        return CanWalkOn(floor);
    }

    /// <summary>
    /// Returns whether this block is a bottom slab.
    /// </summary>
    public static bool IsBottomSlab(BlockState? block)
    {
        if (block == null) return false;
        if (!block.IsSlab) return false;
        return block.Properties.TryGetValue("type", out var type) && type == "bottom";
    }

    /// <summary>
    /// Returns whether this block is soul sand or soul soil (slows movement).
    /// </summary>
    public static bool IsSoulSand(BlockState? block)
    {
        if (block == null) return false;
        return block.Name.Contains("soul_sand", StringComparison.OrdinalIgnoreCase) ||
               block.Name.Contains("soul_soil", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns whether this block is magma.
    /// </summary>
    public static bool IsMagmaBlock(BlockState? block) => block?.Name.Contains("magma", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Returns whether this block is honey (slows movement and jump).
    /// </summary>
    public static bool IsHoneyBlock(BlockState? block) => block?.Name.Contains("honey_block", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Returns whether this block is slippery (ice).
    /// </summary>
    public static bool IsSlippery(BlockState? block)
    {
        if (block == null) return false;
        return block.Friction > 0.8f; // Ice has 0.98, blue ice has 0.989
    }

    // ===== Physics Property Accessors =====

    /// <summary>
    /// Gets the friction of a block for pathfinding cost calculations.
    /// Higher friction = more slippery (ice).
    /// </summary>
    public static float GetFriction(BlockState? block)
    {
        return block?.Friction ?? 0.6f;
    }

    /// <summary>
    /// Gets the movement speed factor for a block.
    /// Less than 1.0 = slows movement (soul sand, honey).
    /// </summary>
    public static float GetSpeedFactor(BlockState? block)
    {
        return block?.SpeedFactor ?? 1.0f;
    }

    /// <summary>
    /// Gets the jump factor for a block.
    /// Less than 1.0 = reduced jump (honey).
    /// </summary>
    public static float GetJumpFactor(BlockState? block)
    {
        return block?.JumpFactor ?? 1.0f;
    }

    /// <summary>
    /// Returns whether walking on this block will slow movement.
    /// </summary>
    public static bool SlowsMovement(BlockState? block)
    {
        if (block == null) return false;
        return block.SpeedFactor < 1.0f;
    }

    /// <summary>
    /// Returns whether jumping on this block is reduced.
    /// </summary>
    public static bool ReducesJump(BlockState? block)
    {
        if (block == null) return false;
        return block.JumpFactor < 1.0f;
    }

    // ===== Coordinate Utilities =====

    /// <summary>
    /// Gets the center of a block for targeting purposes.
    /// </summary>
    public static (double X, double Y, double Z) GetBlockCenter(int x, int y, int z)
    {
        return (x + 0.5, y + 0.5, z + 0.5);
    }

    /// <summary>
    /// Calculates yaw angle to look at a target position.
    /// </summary>
    public static float CalculateYaw(double fromX, double fromZ, double toX, double toZ)
    {
        var dx = toX - fromX;
        var dz = toZ - fromZ;
        var yaw = Math.Atan2(-dx, dz) * (180.0 / Math.PI);
        return (float)yaw;
    }

    /// <summary>
    /// Calculates pitch angle to look at a target position.
    /// </summary>
    public static float CalculatePitch(double fromX, double fromY, double fromZ, double toX, double toY, double toZ)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var dz = toZ - fromZ;
        var horizontalDist = Math.Sqrt(dx * dx + dz * dz);
        var pitch = -Math.Atan2(dy, horizontalDist) * (180.0 / Math.PI);
        return (float)pitch;
    }

    /// <summary>
    /// Calculates both yaw and pitch to look at a target position.
    /// </summary>
    public static (float Yaw, float Pitch) CalculateRotation(double fromX, double fromY, double fromZ, double toX, double toY, double toZ)
    {
        var yaw = CalculateYaw(fromX, fromZ, toX, toZ);
        var pitch = CalculatePitch(fromX, fromY, fromZ, toX, toY, toZ);
        return (yaw, pitch);
    }

    // ===== Movement Input Helpers =====

    /// <summary>
    /// Gets the direction to move towards a target block.
    /// Returns normalized direction vector.
    /// </summary>
    public static (double X, double Z) GetDirectionTo(double fromX, double fromZ, double toX, double toZ)
    {
        var dx = toX - fromX;
        var dz = toZ - fromZ;
        var length = Math.Sqrt(dx * dx + dz * dz);
        if (length < 0.01) return (0, 0);
        return (dx / length, dz / length);
    }

    // ===== Block Breaking Safety Checks =====

    /// <summary>
    /// Returns whether we should avoid breaking this block.
    /// Checks for ice (becomes water), infested blocks, and adjacent hazards.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:64-78
    /// </summary>
    public static bool AvoidBreaking(CalculationContext context, int x, int y, int z, BlockState? state)
    {
        if (state == null) return true;

        var name = state.Name.ToLowerInvariant();
        
        // Ice becomes water, which can mess up the path
        if (name.Contains("ice") && !name.Contains("packed")) return true;
        
        // Infested blocks spawn silverfish
        if (name.Contains("infested")) return true;

        // Check adjacent blocks for hazards
        return AvoidAdjacentBreaking(context, x, y + 1, z, true) ||
               AvoidAdjacentBreaking(context, x + 1, y, z, false) ||
               AvoidAdjacentBreaking(context, x - 1, y, z, false) ||
               AvoidAdjacentBreaking(context, x, y, z + 1, false) ||
               AvoidAdjacentBreaking(context, x, y, z - 1, false);
    }

    /// <summary>
    /// Returns whether we should avoid breaking a block adjacent to this position.
    /// Checks for falling blocks and liquids that could flow.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:80-107
    /// </summary>
    public static bool AvoidAdjacentBreaking(CalculationContext context, int x, int y, int z, bool directlyAbove)
    {
        var state = context.GetBlockState(x, y, z);
        if (state == null) return false;

        // Check for falling blocks that would fall if we break support
        if (!directlyAbove && IsFallingBlock(state))
        {
            var below = context.GetBlockState(x, y - 1, z);
            if (below == null || IsAir(below) || !CanWalkOn(below))
            {
                return true; // Would cause the falling block to fall
            }
        }

        // Liquids can flow when adjacent blocks are broken
        if (IsLiquid(state))
        {
            if (directlyAbove) return true; // Never break directly below liquid
            
            // Source blocks (level 0) like to flow horizontally
            // Non-source will flow down if possible
            var below = context.GetBlockState(x, y - 1, z);
            if (below == null || !IsLiquid(below))
            {
                return true; // Liquid would flow out
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether this block is fully passable (no collision AND no slowing).
    /// Unlike CanWalkThrough, this excludes doors, gates, water, ladders, vines, cobwebs.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:228-289
    /// </summary>
    public static bool FullyPassable(BlockState? block)
    {
        if (block == null) return false;
        if (IsAir(block)) return true;

        var name = block.Name.ToLowerInvariant();

        // Exclude blocks that are passable but slow/interact
        if (name.Contains("fire") ||
            name.Contains("tripwire") ||
            name.Contains("cobweb") ||
            name.Contains("vine") ||
            name.Contains("ladder") ||
            name.Contains("cocoa") ||
            name.Contains("door") ||
            name.Contains("fence_gate") ||
            name.Contains("snow_layer") ||
            name.Contains("trapdoor") ||
            name.Contains("end_portal") ||
            name.Contains("skull") ||
            name.Contains("shulker") ||
            block.IsLiquid)
        {
            return false;
        }

        // If it has no collision, it's fully passable
        return !block.HasCollision;
    }

    /// <summary>
    /// Returns whether we can place a block against this block.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:567-587
    /// </summary>
    public static bool CanPlaceAgainst(BlockState? block)
    {
        if (block == null) return false;

        // Need a solid face to place against
        return IsBlockNormalCube(block) || 
               block.Name.Contains("glass", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns whether this block is a normal full cube (solid on all sides).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:725-741
    /// </summary>
    public static bool IsBlockNormalCube(BlockState? block)
    {
        if (block == null) return false;
        if (!block.HasCollision) return false;

        var name = block.Name.ToLowerInvariant();
        
        // Exclude known non-cube blocks
        if (name.Contains("bamboo") ||
            name.Contains("piston") ||
            name.Contains("scaffolding") ||
            name.Contains("shulker") ||
            name.Contains("dripstone") ||
            name.Contains("amethyst") ||
            name.Contains("slab") ||
            name.Contains("stair") ||
            name.Contains("fence") ||
            name.Contains("wall") ||
            name.Contains("pane") ||
            name.Contains("door") ||
            name.Contains("trapdoor") ||
            name.Contains("chest") ||
            name.Contains("anvil") ||
            name.Contains("enchanting") ||
            name.Contains("brewing") ||
            name.Contains("cauldron") ||
            name.Contains("hopper") ||
            name.Contains("bed") ||
            name.Contains("cake") ||
            name.Contains("candle") ||
            name.Contains("carpet") ||
            name.Contains("snow") ||
            name.Contains("button") ||
            name.Contains("lever") ||
            name.Contains("pressure_plate") ||
            name.Contains("sign") ||
            name.Contains("banner") ||
            name.Contains("torch") ||
            name.Contains("lantern") ||
            name.Contains("chain") ||
            name.Contains("rod") ||
            name.Contains("flower") ||
            name.Contains("sapling") ||
            name.Contains("crop") ||
            name.Contains("wheat") ||
            name.Contains("carrot") ||
            name.Contains("potato") ||
            name.Contains("beetroot") ||
            name.Contains("melon_stem") ||
            name.Contains("pumpkin_stem") ||
            name.Contains("cocoa") ||
            name.Contains("chorus") ||
            name.Contains("dead_bush") ||
            name.Contains("grass") && !name.Contains("grass_block") ||
            name.Contains("fern") ||
            name.Contains("seagrass") ||
            name.Contains("kelp") ||
            name.Contains("pickle") ||
            name.Contains("coral") && !name.Contains("coral_block") ||
            name.Contains("head") ||
            name.Contains("skull"))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Calculates the mining duration in ticks for a block.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:589-622
    /// </summary>
    public static double GetMiningDurationTicks(CalculationContext context, int x, int y, int z, bool includeFalling)
    {
        var state = context.GetBlockState(x, y, z);
        return GetMiningDurationTicks(context, x, y, z, state, includeFalling);
    }

    /// <summary>
    /// Calculates the mining duration in ticks for a block.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:593-622
    /// </summary>
    public static double GetMiningDurationTicks(CalculationContext context, int x, int y, int z, BlockState? state, bool includeFalling)
    {
        if (state == null) return 0;

        // If we can walk through, no mining needed
        if (CanWalkThrough(state)) return 0;

        // Can't mine liquids
        if (state.IsLiquid) return ActionCosts.CostInf;

        // Check if we should avoid breaking this block
        if (AvoidBreaking(context, x, y, z, state)) return ActionCosts.CostInf;

        // Get mining speed from tool
        float toolSpeed = context.GetBestToolSpeed?.Invoke(state) ?? 1.0f;
        if (toolSpeed <= 0) return ActionCosts.CostInf;

        // Calculate base mining time
        double result = ActionCosts.CalculateMiningDuration(toolSpeed, state.DestroySpeed, true);
        if (result >= ActionCosts.CostInf) return ActionCosts.CostInf;

        // Add cost for falling blocks above if requested
        if (includeFalling)
        {
            var above = context.GetBlockState(x, y + 1, z);
            if (IsFallingBlock(above))
            {
                result += GetMiningDurationTicks(context, x, y + 1, z, above, true);
            }
        }

        return result;
    }
}
