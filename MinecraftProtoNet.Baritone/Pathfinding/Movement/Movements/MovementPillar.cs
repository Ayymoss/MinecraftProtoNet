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
/// Based on Baritone's MovementPillar.java.
/// </summary>
public class MovementPillar : MovementBase
{
    private int _ticksWithoutProgress;
    private bool _blockPlaced; // Track if block has been placed this movement

    public MovementPillar(int srcX, int srcY, int srcZ)
        : base(srcX, srcY, srcZ, srcX, srcY + 1, srcZ, MoveDirection.Pillar)
    {
    }

    /// <summary>
    /// Calculates cost for pillaring up one block.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementPillar.java:58-139
    /// </summary>
    public override double CalculateCost(CalculationContext context)
    {
        var x = Source.X;
        var y = Source.Y;
        var z = Source.Z;

        // Baritone lines 59-70: Basic checks
        var fromState = context.GetBlockState(x, y, z);
        bool ladder = MovementHelper.IsClimbable(fromState);
        var fromDown = context.GetBlockState(x, y - 1, z);

        if (!ladder)
        {
            // Can't pillar from a ladder/vine onto something that isn't climbable
            if (MovementHelper.IsClimbable(fromDown))
            {
                return ActionCosts.CostInf;
            }
            // Can't pillar from bottom slab onto non-ladder (Baritone line 67-69)
            if (MovementHelper.IsBottomSlab(fromDown))
            {
                return ActionCosts.CostInf;
            }
        }

        // Baritone line 74-78: Check block at y+2 (the one we need to move into headspace)
        var toBreak = context.GetBlockState(x, y + 2, z);
        
        // Fence gates are always impossible (issue #172)
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
                return ActionCosts.LadderUpOneCost; // Allow ascending water columns
            }
        }

        // Baritone lines 86-96: Calculate place cost (if not ladder)
        double placeCost = 0;
        if (!ladder)
        {
            if (!context.AllowPlace || !context.HasThrowaway)
            {
                return ActionCosts.CostInf;
            }
            placeCost = context.PlaceBlockCost;
            
            // Can't place if floor below isn't solid
            if (!MovementHelper.CanWalkOn(fromDown))
            {
                return ActionCosts.CostInf;
            }
            
            // Slight penalty for pillaring on air (Baritone line 93-95)
            if (MovementHelper.IsAir(fromDown))
            {
                placeCost += 0.1;
            }
        }

        // Baritone lines 97-106: Check for liquid standing conditions
        if (MovementHelper.IsLiquid(fromState) && !MovementHelper.CanWalkOn(fromDown))
        {
            return ActionCosts.CostInf; // Can't pillar while standing in water without floor
        }

        // Baritone line 107-133: Calculate mining cost for block at y+2
        float toolSpeed = context.GetBestToolSpeed?.Invoke(toBreak) ?? 1.0f;
        double hardness = toBreak != null 
            ? ActionCosts.CalculateMiningDuration(toolSpeed, toBreak.DestroySpeed, true)
            : 0;
        if (hardness >= ActionCosts.CostInf)
        {
            // Block is unbreakable and we can't walk through it
            if (!MovementHelper.CanWalkThrough(toBreak))
            {
                if (!context.AllowBreak)
                {
                    return ActionCosts.CostInf;
                }
                return ActionCosts.CostInf; // Truly unbreakable
            }
            hardness = 0; // Can walk through, no mining needed
        }

        // If we can walk through the block, no mining needed
        if (MovementHelper.CanWalkThrough(toBreak))
        {
            hardness = 0;
        }

        // Baritone lines 112-113: Ladders/vines at y+2 don't need breaking (we climb them)
        if (hardness != 0 && MovementHelper.IsClimbable(toBreak))
        {
            hardness = 0;
        }

        // Baritone lines 115-124: Check for falling blocks above what we're breaking
        if (hardness != 0)
        {
            var blockAboveBreak = context.GetBlockState(x, y + 3, z);
            if (MovementHelper.IsFallingBlock(blockAboveBreak))
            {
                var srcUp = context.GetBlockState(x, y + 1, z);
                // Chain of falling blocks is ok, but a falling block on top of non-falling is dangerous
                if (!MovementHelper.IsFallingBlock(toBreak) || !MovementHelper.IsFallingBlock(srcUp))
                {
                    return ActionCosts.CostInf;
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

    public override MovementState UpdateState(Entity entity, Level level)
    {
        // Clear click inputs from previous tick to prevent repeated placements
        State.RightClick = false;
        State.LeftClick = false;
        State.PlaceBlockTarget = null;
        
        var feet = GetFeetPosition(entity);
        var fromState = level.GetBlockAt(Source.X, Source.Y, Source.Z);
        bool ladder = MovementHelper.IsClimbable(fromState);

        // Check if block is already there (Baritone line 204)
        bool blockIsThere = MovementHelper.CanWalkOn(level.GetBlockAt(Source.X, Source.Y, Source.Z)) || _blockPlaced;

        // Check if we've arrived (Baritone line 272)
        if (feet.Y >= Destination.Y && blockIsThere && (entity.IsOnGround || ladder))
        {
            State.ClearInputs();
            State.Status = MovementStatus.Success;
            return State;
        }

        State.Status = MovementStatus.Running;

        if (ladder)
        {
            // Climb logic
            if (fromState == null)
            {
                State.Status = MovementStatus.Failed;
                return State;
            }
            var against = GetLadderFacingOffset(fromState, Source);
            if (against == null)
            {
                State.Status = MovementStatus.Failed;
                return State;
            }
            State.SetTarget(MovementHelper.CalculateYaw(entity.Position.X, entity.Position.Z, against.Value.X + 0.5, against.Value.Z + 0.5), entity.YawPitch.Y);
            State.MoveForward = true; // Hold forward against ladder to climb
        }
        else
        {
            // Baritone line 232: Always sneak while pillaring
            State.Sneak = true;
            
            // Calculate distance from center (Baritone lines 235-237)
            double diffX = entity.Position.X - (Destination.X + 0.5);
            double diffZ = entity.Position.Z - (Destination.Z + 0.5);
            double dist = Math.Sqrt(diffX * diffX + diffZ * diffZ);
            double flatMotion = Math.Sqrt(entity.Velocity.X * entity.Velocity.X + entity.Velocity.Z * entity.Velocity.Z);

            // Look at source block (for placing) - use current yaw, just adjust pitch
            var rotation = MovementHelper.CalculateRotation(
                entity.Position.X, entity.Position.Y + 1.6, entity.Position.Z, // eye position
                Source.X + 0.5, Source.Y + 0.5, Source.Z + 0.5); // block center
            
            if (!ladder)
            {
                // Set target pitch to look down at block (Baritone line 201)
                State.SetTarget(entity.YawPitch.X, rotation.Pitch);
            }

            // Baritone lines 239-251: Movement and jump logic
            if (dist > 0.17)
            {
                // Too far from center - move forward to recenter
                State.MoveForward = true;
                // Also update yaw when moving
                State.SetTarget(rotation.Yaw, rotation.Pitch);
            }
            else if (flatMotion < 0.05)
            {
                // Centered and not moving much - can jump if below destination
                State.Jump = entity.Position.Y < Destination.Y;
            }

            // Baritone lines 254-268: Block placement logic
            if (!blockIsThere && !_blockPlaced)
            {
                // Check if we should place (Baritone line 265)
                // Conditions: crouching, looking at block, above destination
                if (entity.IsSneaking && entity.Position.Y > Destination.Y + 0.1)
                {
                    // Check if we're looking at the source block (approximate check)
                    // In Baritone: ctx.isLookingAt(src.below()) || ctx.isLookingAt(src)
                    // We'll use pitch > 45 degrees as a proxy for looking down
                    if (entity.YawPitch.Y > 45)
                    {
                        Log.Debug("[Pillar] Placing block at ({X}, {Y}, {Z})", Source.X, Source.Y, Source.Z);
                        State.RightClick = true;
                        State.PlaceBlockTarget = (Source.X, Source.Y, Source.Z);
                        _blockPlaced = true; // Only place once!
                    }
                }
            }
        }

        _ticksWithoutProgress++;
        if (_ticksWithoutProgress > 100)
        {
            State.ClearInputs();
            State.Status = MovementStatus.Failed;
        }

        return State;
    }

    private (int X, int Y, int Z)? GetLadderFacingOffset(BlockState? state, (int X, int Y, int Z) pos)
    {
        // For ladders, we want to look towards the block they are attached to
        if (state != null && state.Properties.TryGetValue("facing", out var facing))
        {
            return facing switch
            {
                "north" => (pos.X, pos.Y, pos.Z - 1),
                "south" => (pos.X, pos.Y, pos.Z + 1),
                "west" => (pos.X - 1, pos.Y, pos.Z),
                "east" => (pos.X + 1, pos.Y, pos.Z),
                _ => null
            };
        }
        // For vines, we just try to find a solid neighbor
        return (pos.X + 1, pos.Y, pos.Z); // Simplified vine logic
    }

    public override void Reset()
    {
        base.Reset();
        _ticksWithoutProgress = 0;
        _blockPlaced = false; // Reset placement tracking
    }
}
