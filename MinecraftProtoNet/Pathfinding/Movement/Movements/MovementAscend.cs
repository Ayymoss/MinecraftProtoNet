using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using Serilog;

namespace MinecraftProtoNet.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for jumping up one block.
/// Based on Baritone's MovementAscend.java.
/// </summary>
public class MovementAscend : MovementBase
{
    private int _ticksWithoutProgress;

    public MovementAscend(int srcX, int srcY, int srcZ, int destX, int destZ, MoveDirection direction)
        : base(srcX, srcY, srcZ, destX, srcY + 1, destZ, direction)
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

        // Check destination floor (the block we're jumping onto)
        var jumpOnto = context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(jumpOnto))
        {
            // Need to place a block to jump onto
            if (!context.HasThrowaway || !MovementHelper.IsReplaceable(jumpOnto))
            {
                return ActionCosts.CostInf;
            }
            // Can place - add placement cost
            Cost = ActionCosts.JumpOneBlockCost + context.PlaceBlockCost + context.JumpPenalty;
            return Cost;
        }

        // Check clearance at destination (2 blocks high)
        var destBody = context.GetBlockState(destX, destY, destZ);
        var destHead = context.GetBlockState(destX, destY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(destBody) || !MovementHelper.CanWalkThrough(destHead))
        {
            // Would need to break blocks
            if (!context.AllowBreak)
            {
                return ActionCosts.CostInf;
            }
            Cost = ActionCosts.JumpOneBlockCost * 3; // Placeholder
            return Cost;
        }

        // Check jump clearance above current position
        var jumpSpace = context.GetBlockState(srcX, srcY + 2, srcZ);
        if (!MovementHelper.CanWalkThrough(jumpSpace))
        {
            // Would bump head
            if (!context.AllowBreak)
            {
                return ActionCosts.CostInf;
            }
            Cost = ActionCosts.JumpOneBlockCost * 2; // Placeholder
            return Cost;
        }

        // Can't jump from ladder/vine
        var srcFloor = context.GetBlockState(srcX, srcY - 1, srcZ);
        if (MovementHelper.IsClimbable(srcFloor))
        {
            return ActionCosts.CostInf;
        }

        // Can't jump from bottom slab to non-slab
        if (MovementHelper.IsBottomSlab(srcFloor) && !MovementHelper.IsBottomSlab(jumpOnto))
        {
            return ActionCosts.CostInf;
        }

        // Calculate cost
        double cost;
        if (MovementHelper.IsBottomSlab(jumpOnto))
        {
            // Walking into a slab doesn't require a full jump
            cost = MovementHelper.IsBottomSlab(srcFloor)
                ? Math.Max(ActionCosts.JumpOneBlockCost, ActionCosts.WalkOneBlockCost) + context.JumpPenalty
                : ActionCosts.WalkOneBlockCost;
        }
        else
        {
            cost = Math.Max(ActionCosts.JumpOneBlockCost, ActionCosts.WalkOneBlockCost) + context.JumpPenalty;
        }

        // Soul sand/magma penalties
        if (MovementHelper.IsSoulSand(jumpOnto))
        {
            cost = ActionCosts.WalkOneOverSoulSandCost + context.JumpPenalty;
        }
        else if (MovementHelper.IsMagmaBlock(jumpOnto))
        {
            cost = ActionCosts.SneakOneBlockCost + context.JumpPenalty;
        }

        Cost = cost;
        return Cost;
    }

    public override MovementState UpdateState(Entity entity, Level level)
    {
        var feet = GetFeetPosition(entity);

        // Check if we've fallen below start
        if (feet.Y < Source.Y)
        {
            State.ClearInputs();
            State.Status = MovementStatus.Unreachable;
            return State;
        }

        // Check if we've arrived
        if (feet.X == Destination.X && feet.Y == Destination.Y && feet.Z == Destination.Z)
        {
            State.Status = MovementStatus.Success;
            return State;
        }

        State.Status = MovementStatus.Running;
        MoveTowards(entity);
        // NOTE: Sprint is controlled by PathExecutor, not individual movements!
        
        // Baritone line 194: Sneak on magma blocks to avoid damage
        var jumpOnto = level.GetBlockAt(Destination.X, Destination.Y - 1, Destination.Z);
        if (jumpOnto?.Name == "minecraft:magma_block")
        {
            State.Sneak = true;
        }

        // Jump timing logic based on Baritone
        var xAxis = Math.Abs(Source.X - Destination.X); // 1 or 0
        var zAxis = Math.Abs(Source.Z - Destination.Z); // 1 or 0
        
        // flatDistToNext: distance to target along its own axis
        double flatDistToNext = xAxis * Math.Abs((Destination.X + 0.5) - entity.Position.X) 
                              + zAxis * Math.Abs((Destination.Z + 0.5) - entity.Position.Z);
        
        // sideDist: lateral distance from the center line of movement
        double sideDist = zAxis * Math.Abs((Destination.X + 0.5) - entity.Position.X) 
                        + xAxis * Math.Abs((Destination.Z + 0.5) - entity.Position.Z);

        // Baritone lines 210-213: Check lateral motion before jumping
        // This prevents jumping when we have sideways momentum that could cause overshooting
        double lateralMotion = xAxis * entity.Velocity.Z + zAxis * entity.Velocity.X;
        if (Math.Abs(lateralMotion) > 0.1)
        {
            // Don't jump yet - wait for lateral motion to settle
            Log.Verbose("[Ascend] Waiting for lateral motion to settle: {LateralMotion:F3}", lateralMotion);
            return State;
        }

        if (HeadBonkClear(level))
        {
            Log.Verbose("[Ascend] Jumping (HeadBonkClear)");
            State.Jump = true;
        }
        else if (flatDistToNext <= 1.2 && sideDist <= 0.2)
        {
            Log.Verbose("[Ascend] Jumping (Tunnel check passed: flat={FlatDist:F2})", flatDistToNext);
            // If not clear above (e.g. tunnel), wait until close to jump
            State.Jump = true;
        }
        else
        {
            Log.Verbose("[Ascend] Waiting to jump (flat={FlatDist:F2}, side={SideDist:F2})", flatDistToNext, sideDist);
        }

        _ticksWithoutProgress++;
        if (_ticksWithoutProgress > 40)
        {
            State.ClearInputs();
            State.Status = MovementStatus.Failed;
        }

        return State;
    }

    public override void Reset()
    {
        base.Reset();
        _ticksWithoutProgress = 0;
    }

    private bool HeadBonkClear(Level level)
    {
        // Baritone checks neighbors of src.above(2)
        var headY = Source.Y + 2;
        
        // Check 4 cardinal directions at head height to see if we might bonk
        // Source: MovementAscend.java:229
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (Math.Abs(dx) == Math.Abs(dz)) continue; // Only cardinals
                
                var state = level.GetBlockAt(Source.X + dx, headY, Source.Z + dz);
                if (!MovementHelper.CanWalkThrough(state))
                {
                    return false;
                }
            }
        }
        return true;
    }
}
