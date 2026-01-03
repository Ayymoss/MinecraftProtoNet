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
    /// Returns whether Frost Walker can be used on this block state.
    /// Frost Walker allows walking on water by freezing it into frosted ice.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:499-503
    /// </summary>
    /// <param name="context">Calculation context with frost walker level</param>
    /// <param name="state">Block state to check (should be water)</param>
    /// <returns>True if frost walker can be used (frost walker level > 0 and block is water source)</returns>
    public static bool CanUseFrostWalker(CalculationContext context, BlockState? state)
    {
        if (state == null) return false;
        if (context.FrostWalker == 0) return false;
        if (!IsWater(state)) return false;
        
        // Baritone checks: state == FrostedIceBlock.meltsInto() && state.getValue(LiquidBlock.LEVEL) == 0
        // FrostedIceBlock.meltsInto() returns water with level 0 (source block)
        // Check if this is a water source block (level property should be 0 or absent for source)
        if (state.Properties.TryGetValue("level", out var levelStr))
        {
            if (int.TryParse(levelStr, out var level) && level != 0)
            {
                return false; // Not a source block
            }
        }
        
        return true;
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
    /// Returns whether this block is a lily pad.
    /// Reference: Baritone MovementPillar.cost() line 103
    /// </summary>
    public static bool IsLilyPad(BlockState? block) => block?.Name.Contains("lily_pad", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Returns whether this block is a carpet.
    /// Reference: Baritone MovementPillar.cost() line 103
    /// </summary>
    public static bool IsCarpet(BlockState? block) => block?.Name.Contains("carpet", StringComparison.OrdinalIgnoreCase) == true;

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
    /// Returns whether the block at the given position is fully passable.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:fullyPassable(context, x, y, z)
    /// </summary>
    public static bool FullyPassable(CalculationContext context, int x, int y, int z)
    {
        return FullyPassable(context.GetBlockState(x, y, z));
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

    /// <summary>
    /// Checks if a door is passable from the player's position.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:325-336
    /// </summary>
    public static bool IsDoorPassable(CalculationContext context, int doorX, int doorY, int doorZ, int playerX, int playerY, int playerZ)
    {
        if (doorX == playerX && doorY == playerY && doorZ == playerZ)
        {
            return false; // Can't pass through if standing on the door
        }

        var state = context.GetBlockState(doorX, doorY, doorZ);
        if (state == null || !IsDoor(state))
        {
            return true; // Not a door, assume passable
        }

        return IsHorizontalBlockPassable(context, doorX, doorY, doorZ, state, playerX, playerY, playerZ);
    }

    /// <summary>
    /// Checks if a fence gate is passable (open).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:338-349
    /// </summary>
    public static bool IsGatePassable(CalculationContext context, int gateX, int gateY, int gateZ, int playerX, int playerY, int playerZ)
    {
        if (gateX == playerX && gateY == playerY && gateZ == playerZ)
        {
            return false; // Can't pass through if standing on the gate
        }

        var state = context.GetBlockState(gateX, gateY, gateZ);
        if (state == null || !IsFenceGate(state))
        {
            return true; // Not a gate, assume passable
        }

        // Check if gate is open (property "open" should be "true")
        if (state.Properties.TryGetValue("open", out var openStr))
        {
            return openStr == "true";
        }

        return false; // If no open property, assume closed
    }

    /// <summary>
    /// Checks if a horizontal block (like a door) is passable based on facing direction and open state.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:351-369
    /// </summary>
    public static bool IsHorizontalBlockPassable(CalculationContext context, int blockX, int blockY, int blockZ, BlockState? blockState, int playerX, int playerY, int playerZ)
    {
        if (blockState == null) return true;
        if (blockX == playerX && blockY == playerY && blockZ == playerZ)
        {
            return false;
        }

        // Get facing direction (property "facing": "north", "south", "east", "west")
        if (!blockState.Properties.TryGetValue("facing", out var facingStr))
        {
            return true; // No facing property, assume passable
        }

        // Get open state (property "open": "true"/"false")
        if (!blockState.Properties.TryGetValue("open", out var openStr))
        {
            return false; // No open property, assume closed
        }

        bool isOpen = openStr == "true";

        // Determine player's relative position to block
        int dx = playerX - blockX;
        int dz = playerZ - blockZ;

        // Determine axis based on facing direction
        bool facingAxisZ = facingStr == "north" || facingStr == "south";
        bool playerAxisZ = dz != 0; // Player is north or south of block

        // Block is passable if: (facing axis == player axis) == open
        // If facing Z-axis and player is on Z-axis: passable when open
        // If facing X-axis and player is on X-axis: passable when open
        return (facingAxisZ == playerAxisZ) == isOpen;
    }

    // ===== Tool Switching =====

    /// <summary>
    /// Switches to the best tool for mining the given block.
    /// This is a simplified version - full implementation requires inventory infrastructure.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:635-650
    /// </summary>
    /// <param name="block">The block state to mine</param>
    /// <param name="context">Calculation context with tool speed callback</param>
    /// <param name="switchToolCallback">Callback to actually switch tool (takes slot index 0-8)</param>
    /// <param name="getToolSpeedCallback">Callback to get tool speed for a slot (takes slot index, returns speed multiplier)</param>
    /// <param name="preferSilkTouch">Whether to prefer silk touch tools</param>
    public static void SwitchToBestToolFor(
        BlockState block, 
        CalculationContext context,
        Action<int>? switchToolCallback,
        Func<int, float>? getToolSpeedCallback,
        bool preferSilkTouch = false)
    {
        if (switchToolCallback == null || getToolSpeedCallback == null)
        {
            return; // No infrastructure available, skip tool switching
        }

        // Find best slot (0-8 hotbar slots)
        int bestSlot = 0;
        float highestSpeed = float.NegativeInfinity;

        for (int slot = 0; slot < 9; slot++)
        {
            float speed = getToolSpeedCallback(slot);
            if (speed > highestSpeed)
            {
                highestSpeed = speed;
                bestSlot = slot;
            }
        }

        // Switch to best tool
        switchToolCallback(bestSlot);
    }

    /// <summary>
    /// Switches to the best tool for mining the block at the given position.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:635-636
    /// </summary>
    public static void SwitchToBestToolFor(
        CalculationContext context,
        int x, int y, int z,
        Action<int>? switchToolCallback,
        Func<int, float>? getToolSpeedCallback,
        bool preferSilkTouch = false)
    {
        var block = context.GetBlockState(x, y, z);
        if (block == null) return;
        SwitchToBestToolFor(block, context, switchToolCallback, getToolSpeedCallback, preferSilkTouch);
    }

    // ===== Block Placement =====

    /// <summary>
    /// Result of attempting to place a block.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:799-801
    /// </summary>
    public enum PlaceResult
    {
        /// <summary>
        /// Ready to place (looking at correct position, item selected).
        /// </summary>
        ReadyToPlace,
        
        /// <summary>
        /// Attempting to place (rotating towards position).
        /// </summary>
        Attempting,
        
        /// <summary>
        /// Cannot place (no valid position or no blocks available).
        /// </summary>
        NoOption
    }

    /// <summary>
    /// Attempts to place a block at the given position.
    /// Simplified version - requires infrastructure callbacks for full functionality.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java:743-797
    /// </summary>
    /// <param name="state">Movement state to update with placement actions</param>
    /// <param name="context">Calculation context</param>
    /// <param name="placeAtX">X coordinate to place block</param>
    /// <param name="placeAtY">Y coordinate to place block</param>
    /// <param name="placeAtZ">Z coordinate to place block</param>
    /// <param name="playerX">Player X position</param>
    /// <param name="playerY">Player Y position (feet)</param>
    /// <param name="playerZ">Player Z position</param>
    /// <param name="preferDown">Whether to prefer placing from below</param>
    /// <param name="wouldSneak">Whether player should sneak while placing</param>
    /// <param name="selectThrowawayCallback">Callback to select throwaway block from inventory (returns success)</param>
    /// <returns>PlaceResult indicating the placement state</returns>
    public static PlaceResult AttemptToPlaceABlock(
        MovementState state,
        CalculationContext context,
        int placeAtX, int placeAtY, int placeAtZ,
        double playerX, double playerY, double playerZ,
        bool preferDown,
        bool wouldSneak,
        Func<bool>? selectThrowawayCallback)
    {
        if (selectThrowawayCallback == null || !selectThrowawayCallback())
        {
            state.SetStatus(MovementStatus.Unreachable);
            return PlaceResult.NoOption;
        }

        // Check if we can place against any adjacent block
        // Try directions: down, north, south, east, west (HORIZONTALS_BUT_ALSO_DOWN)
        var directions = new[]
        {
            (0, -1, 0), // down
            (0, 0, -1), // north
            (0, 0, 1),  // south
            (1, 0, 0),  // east
            (-1, 0, 0)  // west
        };

        bool found = false;
        int startIndex = preferDown ? 0 : 1; // Start with down if preferDown, else skip it
        int endIndex = preferDown ? directions.Length : directions.Length;

        for (int i = startIndex; i < endIndex; i++)
        {
            var (dx, dy, dz) = directions[i];
            var againstX = placeAtX + dx;
            var againstY = placeAtY + dy;
            var againstZ = placeAtZ + dz;

            if (CanPlaceAgainst(context.GetBlockState(againstX, againstY, againstZ)))
            {
                // Calculate rotation to look at the face center
                double faceX = (placeAtX + againstX + 1.0) * 0.5;
                double faceY = (placeAtY + againstY + 0.5) * 0.5;
                double faceZ = (placeAtZ + againstZ + 1.0) * 0.5;

                // Calculate yaw and pitch to look at the face center
                var (yaw, pitch) = CalculateRotation(
                    playerX, playerY + 1.6, playerZ, // Approximate head position
                    faceX, faceY, faceZ
                );

                state.SetTarget(yaw, pitch, force: true);
                state.RightClick = true;
                state.PlaceBlockTarget = (placeAtX, placeAtY, placeAtZ);
                
                if (wouldSneak)
                {
                    state.Sneak = true;
                }

                found = true;

                if (!preferDown)
                {
                    // If not preferring down, take first valid option
                    break;
                }
                // If preferring down, continue to find the last (preferred) option
            }
        }

        if (found)
        {
            return wouldSneak && state.Sneak ? PlaceResult.ReadyToPlace : PlaceResult.Attempting;
        }

        state.SetStatus(MovementStatus.Unreachable);
        return PlaceResult.NoOption;
    }
}
