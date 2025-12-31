using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for stepping/falling down one block.
/// Based on Baritone's MovementDescend.java.
/// </summary>
public class MovementDescend : MovementBase
{
    private int _numTicks;
    
    /// <summary>
    /// When true, forces safe mode for this movement.
    /// Can be set by PathExecutor when context requires it.
    /// </summary>
    public bool ForceSafeMode { get; set; }

    public MovementDescend(int srcX, int srcY, int srcZ, int destX, int destZ, MoveDirection direction)
        : base(srcX, srcY, srcZ, destX, srcY - 1, destZ, direction)
    {
    }

    public override double CalculateCost(CalculationContext context)
    {
        var destX = Destination.X;
        var destY = Destination.Y;
        var destZ = Destination.Z;
        var srcX = Source.X;
        var srcY = Source.Y;
        var srcZ = Source.Z;

        // Check destination floor
        var destFloor = context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor))
        {
            return ActionCosts.CostInf;
        }

        // Check body and head clearance at destination
        var destBody = context.GetBlockState(destX, destY, destZ);
        var destHead = context.GetBlockState(destX, destY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(destBody) || !MovementHelper.CanWalkThrough(destHead))
        {
            // Need to break blocks
            if (!context.AllowBreak)
            {
                return ActionCosts.CostInf;
            }
            Cost = ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(1) * 3; // Placeholder
            return Cost;
        }

        // Check we can walk off current position (head clearance going forward)
        var forwardHead = context.GetBlockState(destX, srcY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(forwardHead))
        {
            return ActionCosts.CostInf;
        }

        // Check for dangerous blocks
        if (MovementHelper.AvoidWalkingInto(destBody))
        {
            return ActionCosts.CostInf;
        }

        // Calculate cost: walk to edge + fall + center
        var cost = ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(1) + ActionCosts.CenterAfterFallCost;

        // Water check at destination
        if (MovementHelper.IsWater(destBody))
        {
            cost = ActionCosts.WalkOneInWaterCost;
        }

        Cost = cost;
        return Cost;
    }

    public override MovementState UpdateState(Entity entity, Level level)
    {
        var feet = GetFeetPosition(entity);

        // Baritone line 234: fakeDest is dest projected past the actual destination in the same direction
        // This prevents the 180-degree turn when stepping down
        var fakeDest = (
            X: Destination.X * 2 - Source.X,
            Y: Destination.Y,
            Z: Destination.Z * 2 - Source.Z
        );

        // Check if we've arrived (Baritone lines 235-241)
        var playerAtDest = feet.X == Destination.X && feet.Z == Destination.Z;
        var playerAtFakeDest = feet.X == fakeDest.X && feet.Z == fakeDest.Z;
        var closeToGround = entity.Position.Y - Destination.Y < 0.5;
        
        if ((playerAtDest || playerAtFakeDest) && closeToGround && entity.IsOnGround)
        {
            State.ClearInputs();
            State.Status = MovementStatus.Success;
            return State;
        }

        State.Status = MovementStatus.Running;
        
        // Baritone line 242: Check if we should use safe mode
        if (SafeMode())
        {
            // Safe mode: move towards a point between source and destination (83% towards dest)
            // Baritone lines 243-250
            var safeDestX = (Source.X + 0.5) * 0.17 + (Destination.X + 0.5) * 0.83;
            var safeDestZ = (Source.Z + 0.5) * 0.17 + (Destination.Z + 0.5) * 0.83;
            MoveTowardsPoint(entity, safeDestX, safeDestZ);
            State.MoveForward = true;
            return State;
        }

        // Calculate distance from destination and from start (Baritone lines 253-258)
        var diffX = entity.Position.X - (Destination.X + 0.5);
        var diffZ = entity.Position.Z - (Destination.Z + 0.5);
        var distFromDest = Math.Sqrt(diffX * diffX + diffZ * diffZ);

        var fromStartX = entity.Position.X - (Source.X + 0.5);
        var fromStartZ = entity.Position.Z - (Source.Z + 0.5);
        var distFromStart = Math.Sqrt(fromStartX * fromStartX + fromStartZ * fromStartZ);

        // Check if we're falling (Y is below source level)
        var isFalling = entity.Position.Y < Source.Y - 0.1;

        // Baritone lines 262-268: Move towards fakeDest for first 20 ticks while close to start
        if (feet.X != Destination.X || feet.Z != Destination.Z || distFromDest > 0.25)
        {
            _numTicks++;
            
            // CRITICAL: Only update rotation while on ground approaching the edge.
            // Once falling, DO NOT update rotation - maintain the direction we had.
            // This prevents yaw flip when player overshoots fakeDest during fast movement.
            // In real Minecraft, air control is minimal so momentum carries you through.
            if (!isFalling)
            {
                // Still on ground - aim toward fakeDest
                MoveTowardsPoint(entity, fakeDest.X + 0.5, fakeDest.Z + 0.5);
            }
            // While falling: keep MoveForward but don't change rotation
        }

        // Can sprint while on source level (before the fall) and NOT in safe mode
        if (entity.IsOnGround && feet.Y == Source.Y)
        {
            State.Sprint = true;
        }

        // Check for obstructions to break
        // We need to clear 3 blocks at destination: destBody (Y-1), destHead (Y), destAbove (Y+1)
        // Note: Destination.Y is source.Y - 1
        
        // 1. Check destBody (feet level at destination)
        var destBody = level.GetBlockAt(Destination.X, Destination.Y, Destination.Z); 
        if (destBody != null && !MovementHelper.CanWalkThrough(destBody))
        {
            State.Status = MovementStatus.Running;
            State.ClearInputs();
            State.BreakBlockTarget = (Destination.X, Destination.Y, Destination.Z);
            return State;
        }

        // 2. Check destHead (head level at destination)
        var destHead = level.GetBlockAt(Destination.X, Destination.Y + 1, Destination.Z);
        if (destHead != null && !MovementHelper.CanWalkThrough(destHead))
        {
            State.Status = MovementStatus.Running;
            State.ClearInputs();
            State.BreakBlockTarget = (Destination.X, Destination.Y + 1, Destination.Z);
            return State;
        }

        // 3. Check destAbove (above head at destination) - for clearance
        var destAbove = level.GetBlockAt(Destination.X, Destination.Y + 2, Destination.Z);
        if (destAbove != null && !MovementHelper.CanWalkThrough(destAbove))
        {
            State.Status = MovementStatus.Running;
            State.ClearInputs();
            State.BreakBlockTarget = (Destination.X, Destination.Y + 2, Destination.Z);
            return State;
        }

        // Clear any previous break target
        State.BreakBlockTarget = null;

        // Baritone line 260: Sneak on magma blocks to avoid damage
        var blockBelow = level.GetBlockAt(feet.X, feet.Y - 1, feet.Z);
        if (blockBelow?.Name == "minecraft:magma_block")
        {
            State.Sneak = true;
            State.Sprint = false; // Can't sprint while sneaking
        }

        State.MoveForward = true;

        return State;
    }
    
    /// <summary>
    /// Determines if this descend should use safe mode.
    /// Baritone lines 272-289.
    /// </summary>
    public bool SafeMode()
    {
        if (ForceSafeMode)
        {
            return true;
        }
        
        // Note: Full implementation would check blocks ahead like Baritone does
        // For now, we check if SkipToAscend would cause a glitch
        if (SkipToAscend())
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Checks if sprinting through this descend could cause the player to glitch.
    /// Baritone lines 291-293: returns true if dest is blocked but the blocks above are passable.
    /// </summary>
    public bool SkipToAscend()
    {
        // This would need a Level reference to check blocks properly
        // For now, return false as a safe default (doesn't trigger extra safe mode)
        return false;
    }

    /// <summary>
    /// Move towards a specific point (used for fakeDest and safe mode).
    /// </summary>
    private void MoveTowardsPoint(Entity entity, double targetX, double targetZ)
    {
        var yaw = MovementHelper.CalculateYaw(entity.Position.X, entity.Position.Z, targetX, targetZ);
        State.SetTarget(yaw, entity.YawPitch.Y);
        State.MoveForward = true;
    }

    public override void Reset()
    {
        base.Reset();
        _numTicks = 0;
        ForceSafeMode = false;
    }
}

