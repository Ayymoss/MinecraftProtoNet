using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using Serilog;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for towering up one block (place block below and jump).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementPillar.java
/// </summary>
public class MovementPillar(int srcX, int srcY, int srcZ) : MovementBase(srcX, srcY, srcZ, srcX, srcY + 1, srcZ, MoveDirection.Pillar)
{
    /// <summary>
    /// Calculates cost for pillaring up one block.
    /// Reference: Baritone lines 58-139 - cost() method
    /// </summary>
    public override double CalculateCost(CalculationContext context)
    {
        var x = Source.X;
        var y = Source.Y;
        var z = Source.Z;

        // Baritone lines 59-61: Get source state and check for ladder
        var fromState = context.GetBlockState(x, y, z);
        bool ladder = MovementHelper.IsClimbable(fromState);
        var fromDown = context.GetBlockState(x, y - 1, z);

        // Baritone lines 63-70: Basic checks for non-ladder
        if (!ladder)
        {
            // Baritone lines 64-66: Can't pillar from ladder/vine onto non-climbable
            if (MovementHelper.IsClimbable(fromDown))
            {
                return ActionCosts.CostInf;
            }
            // Baritone lines 67-69: Can't pillar from bottom slab onto non-ladder
            if (MovementHelper.IsBottomSlab(fromDown))
            {
                return ActionCosts.CostInf;
            }
        }

        // Baritone lines 74-78: Check block at y+2 for fence gate
        var toBreak = context.GetBlockState(x, y + 2, z);
        if (MovementHelper.IsFenceGate(toBreak))
        {
            return ActionCosts.CostInf;
        }

        // Baritone lines 80-85: Water swimming up check
        if (MovementHelper.IsWater(toBreak) && MovementHelper.IsWater(fromState))
        {
            var srcUp = context.GetBlockState(x, y + 1, z);
            if (MovementHelper.IsWater(srcUp))
            {
                return ActionCosts.LadderUpOneCost;
            }
        }

        // Baritone lines 86-96: Calculate placement cost
        double placeCost = 0;
        if (!ladder)
        {
            placeCost = context.CostOfPlacingAt(x, y, z);
            if (placeCost >= ActionCosts.CostInf)
            {
                return ActionCosts.CostInf;
            }
            // Baritone lines 93-95: Slight penalty for pillaring on air
            if (MovementHelper.IsAir(fromDown))
            {
                placeCost += 0.1;
            }
        }

        // Baritone lines 97-101: Can't pillar from liquid without floor
        if ((MovementHelper.IsLiquid(fromState) && !MovementHelper.CanPlaceAgainst(fromDown)) ||
            (MovementHelper.IsLiquid(fromDown) && context.AssumeWalkOnWater))
        {
            return ActionCosts.CostInf;
        }

        // Baritone lines 103-106: Can't stand on lily pad or carpet over water
        if ((MovementHelper.IsLilyPad(fromState) || MovementHelper.IsCarpet(fromState)) && 
            MovementHelper.IsLiquid(fromDown))
        {
            return ActionCosts.CostInf;
        }

        // Baritone lines 107-109: Get mining hardness
        double hardness = MovementHelper.GetMiningDurationTicks(context, x, y + 2, z, true);
        if (hardness >= ActionCosts.CostInf)
        {
            return ActionCosts.CostInf;
        }

        // Baritone lines 111-133: Handle ladder/vine at y+2 and falling blocks
        if (hardness != 0)
        {
            // Baritone lines 112-113: Ladders/vines don't need breaking
            if (MovementHelper.IsClimbable(toBreak))
            {
                hardness = 0;
            }
            else
            {
                // Baritone lines 115-124: Check for falling blocks above
                var check = context.GetBlockState(x, y + 3, z);
                if (MovementHelper.IsFallingBlock(check))
                {
                    var srcUp = context.GetBlockState(x, y + 1, z);
                    if (!MovementHelper.IsFallingBlock(toBreak) || !MovementHelper.IsFallingBlock(srcUp))
                    {
                        return ActionCosts.CostInf;
                    }
                }
            }
        }

        // Baritone lines 134-138: Final cost calculation
        if (ladder)
        {
            Cost = ActionCosts.LadderUpOneCost + hardness * 5;
        }
        else
        {
            Cost = ActionCosts.JumpOneBlockCost + placeCost + context.JumpPenalty + hardness;
        }

        return Cost;
    }

    /// <summary>
    /// Update movement state each tick.
    /// Reference: Baritone lines 165-271 - updateState() method
    /// </summary>
    public override MovementState UpdateState(Entity entity, Level level)
    {
        // Baritone line 166: super.updateState(state) - not needed, we handle directly
        
        // Baritone lines 167-168: Check if not running
        if (State.Status != MovementStatus.Running)
        {
            State.Status = MovementStatus.Running;
        }

        var feet = GetFeetPosition(entity);

        // Baritone lines 171-173: Check if fallen below start
        if (feet.Y < Source.Y)
        {
            State.ClearInputs();
            State.Status = MovementStatus.Unreachable;
            return State;
        }

        // Baritone lines 175-187: Water swimming logic
        var fromDown = level.GetBlockAt(Source.X, Source.Y, Source.Z);
        if (MovementHelper.IsWater(fromDown) && MovementHelper.IsWater(level.GetBlockAt(Destination.X, Destination.Y, Destination.Z)))
        {
            // Stay centered while swimming up a water column
            var destCenter = (Destination.X + 0.5, Destination.Y + 0.5, Destination.Z + 0.5);
            var rotation = MovementHelper.CalculateRotation(
                entity.Position.X, entity.Position.Y + 1.6, entity.Position.Z,
                destCenter.Item1, destCenter.Item2, destCenter.Item3);
            State.SetTarget(rotation.Yaw, rotation.Pitch);
            
            if (Math.Abs(entity.Position.X - destCenter.Item1) > 0.2 || 
                Math.Abs(entity.Position.Z - destCenter.Item3) > 0.2)
            {
                State.MoveForward = true;
            }
            if (feet.X == Destination.X && feet.Y == Destination.Y && feet.Z == Destination.Z)
            {
                State.Status = MovementStatus.Success;
            }
            return State;
        }

        // Baritone line 188-189: Check ladder/vine
        bool ladder = MovementHelper.IsLadder(fromDown) || MovementHelper.IsVine(fromDown);
        bool vine = MovementHelper.IsVine(fromDown);
        
        // Baritone lines 190-192: Calculate rotation to positionToPlace (source)
        var positionToPlace = (Source.X + 0.5, Source.Y + 0.5, Source.Z + 0.5);
        var rotationToPlace = MovementHelper.CalculateRotation(
            entity.Position.X, entity.Position.Y + 1.6, entity.Position.Z,
            positionToPlace.Item1, positionToPlace.Item2, positionToPlace.Item3);

        // Baritone lines 193-195: Non-ladder: set pitch only
        if (!ladder)
        {
            State.SetTarget(entity.YawPitch.X, rotationToPlace.Pitch);
        }

        // Baritone line 197: Check if block is placed
        bool blockIsThere = MovementHelper.CanWalkOn(level.GetBlockAt(Source.X, Source.Y, Source.Z)) || ladder;

        // Baritone lines 198-218: Ladder climbing logic
        if (ladder)
        {
            var against = GetLadderFacingOffset(fromDown, Source);
            if (against == null)
            {
                Log.Warning("[Pillar] Unable to climb vines. Consider disabling allowVines.");
                State.Status = MovementStatus.Unreachable;
                return State;
            }

            // Baritone lines 205-206: Success check
            var againstAbove = (against.Value.X, against.Value.Y + 1, against.Value.Z);
            if ((feet.X == againstAbove.X && feet.Y == againstAbove.Item2 && feet.Z == againstAbove.Z) ||
                (feet.X == Destination.X && feet.Y == Destination.Y && feet.Z == Destination.Z))
            {
                State.Status = MovementStatus.Success;
                return State;
            }

            // Baritone lines 208-210: Jump from bottom slab
            var srcBelow = level.GetBlockAt(Source.X, Source.Y - 1, Source.Z);
            if (MovementHelper.IsBottomSlab(srcBelow))
            {
                State.Jump = true;
            }

            // Baritone line 217: Move towards ladder attachment
            MoveTowards(entity, against.Value.X, against.Value.Y, against.Value.Z);
            return State;
        }
        else
        {
            // Non-ladder pillar logic

            // Baritone line 226: Sneak timing for NCP compatibility
            State.Sneak = entity.Position.Y > Destination.Y || entity.Position.Y < Source.Y + 0.2;

            // Baritone lines 229-232: Calculate distance and motion
            double diffX = entity.Position.X - (Destination.X + 0.5);
            double diffZ = entity.Position.Z - (Destination.Z + 0.5);
            double dist = Math.Sqrt(diffX * diffX + diffZ * diffZ);
            double flatMotion = Math.Sqrt(entity.Velocity.X * entity.Velocity.X + entity.Velocity.Z * entity.Velocity.Z);

            // Baritone lines 233-241: Movement when too far from center
            if (dist > 0.17)
            {
                State.MoveForward = true;
                // Revise target to both yaw and pitch when moving
                State.SetTarget(rotationToPlace.Yaw, rotationToPlace.Pitch);
            }
            // Baritone lines 242-245: Jump when centered and not moving
            else if (flatMotion < 0.05)
            {
                State.Jump = entity.Position.Y < Destination.Y;
            }

            // Baritone lines 248-262: Block placement logic
            if (!blockIsThere)
            {
                var frState = level.GetBlockAt(Source.X, Source.Y, Source.Z);
                // Baritone lines 252-258: If block in the way, break it
                if (frState != null && !MovementHelper.IsAir(frState) && !MovementHelper.IsReplaceable(frState))
                {
                    State.Jump = false; // breaking is like 5x slower when jumping
                    State.LeftClick = true;
                    State.BreakBlockTarget = (Source.X, Source.Y, Source.Z);
                }
                // Baritone lines 259-261: Place block when ready
                // Note: Use State.Sneak instead of entity.IsSneaking because entity state lags one tick
                else if (State.Sneak && entity.Position.Y > Destination.Y + 0.1)
                {
                    State.RightClick = true;
                    State.PlaceBlockTarget = (Source.X, Source.Y, Source.Z);
                }
            }
        }

        // Baritone lines 266-268: Final success check
        if (feet.X == Destination.X && feet.Y == Destination.Y && feet.Z == Destination.Z && blockIsThere)
        {
            State.Status = MovementStatus.Success;
        }

        return State;
    }

    /// <summary>
    /// Get the block position a ladder is facing (attached to).
    /// Reference: Baritone lines 148-162 - getAgainst() method
    /// </summary>
    private (int X, int Y, int Z)? GetLadderFacingOffset(BlockState? state, (int X, int Y, int Z) pos)
    {
        if (state == null) return null;

        // For ladders, use the facing property
        if (state.Properties.TryGetValue("facing", out var facing))
        {
            return facing switch
            {
                "north" => (pos.X, pos.Y, pos.Z + 1), // facing north = attached to south
                "south" => (pos.X, pos.Y, pos.Z - 1),
                "west" => (pos.X + 1, pos.Y, pos.Z),
                "east" => (pos.X - 1, pos.Y, pos.Z),
                _ => null
            };
        }

        // For vines, find any adjacent solid block
        // Reference: Baritone lines 141-146 - hasAgainst()
        if (MovementHelper.IsVine(state))
        {
            // Check all 4 directions for a solid block
            int[] dx = [1, -1, 0, 0];
            int[] dz = [0, 0, 1, -1];
            for (int i = 0; i < 4; i++)
            {
                // Note: We can't check block state here without level access
                // Return first direction as fallback
                return (pos.X + dx[i], pos.Y, pos.Z + dz[i]);
            }
        }

        return null;
    }

    /// <summary>
    /// Move towards a specific position.
    /// </summary>
    private void MoveTowards(Entity entity, int x, int y, int z)
    {
        var rotation = MovementHelper.CalculateRotation(
            entity.Position.X, entity.Position.Y + 1.6, entity.Position.Z,
            x + 0.5, y + 0.5, z + 0.5);
        State.SetTarget(rotation.Yaw, rotation.Pitch);
        State.MoveForward = true;
    }
}
