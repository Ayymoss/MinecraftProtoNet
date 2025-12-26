using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for diagonal movement (moving diagonally in X and Z).
/// Based on Baritone's MovementDiagonal.java.
/// </summary>
public class MovementDiagonal : MovementBase
{
    private int _ticksWithoutProgress;

    public MovementDiagonal(int srcX, int srcY, int srcZ, int destX, int destY, int destZ, MoveDirection direction)
        : base(srcX, srcY, srcZ, destX, destY, destZ, direction)
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

        var dy = destY - srcY;

        // Check destination floor
        var destFloor = context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor)) return ActionCosts.CostInf;

        // Check body and head clearance at destination
        var destBody = context.GetBlockState(destX, destY, destZ);
        var destHead = context.GetBlockState(destX, destY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(destBody) || !MovementHelper.CanWalkThrough(destHead)) return ActionCosts.CostInf;

        var dx = destX - srcX;
        var dz = destZ - srcZ;

        // Intermediate corner checks
        var cornerABody = context.GetBlockState(srcX + dx, srcY, srcZ);
        var cornerAHead = context.GetBlockState(srcX + dx, srcY + 1, srcZ);
        var cornerBBody = context.GetBlockState(srcX, srcY, srcZ + dz);
        var cornerBHead = context.GetBlockState(srcX, srcY + 1, srcZ + dz);

        bool canA = MovementHelper.CanWalkThrough(cornerABody) && MovementHelper.CanWalkThrough(cornerAHead);
        bool canB = MovementHelper.CanWalkThrough(cornerBBody) && MovementHelper.CanWalkThrough(cornerBHead);
        
        if (!canA && !canB) return ActionCosts.CostInf;

        if (dy > 0) // Ascend
        {
            var srcCeil = context.GetBlockState(srcX, srcY + 2, srcZ);
            if (!MovementHelper.CanWalkThrough(srcCeil)) return ActionCosts.CostInf;

            var cornerACeil = context.GetBlockState(srcX + dx, srcY + 2, srcZ);
            var cornerBCeil = context.GetBlockState(srcX, srcY + 2, srcZ + dz);
            
            bool canACeil = canA && MovementHelper.CanWalkThrough(cornerACeil);
            bool canBCeil = canB && MovementHelper.CanWalkThrough(cornerBCeil);
            
            if (!canACeil && !canBCeil) return ActionCosts.CostInf;
        }

        var multiplier = context.CanSprint ? ActionCosts.SprintMultiplier : 1.0;
        var cost = ActionCosts.WalkOneBlockCost * Math.Sqrt(2) * multiplier;
        
        if (dy > 0) cost += ActionCosts.JumpOneBlockCost + context.JumpPenalty;
        if (dy < 0) cost += ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(1) + ActionCosts.CenterAfterFallCost;

        Cost = cost;
        return Cost;
    }

    public override MovementState UpdateState(Entity entity, Level level)
    {
        if (HasReachedDestination(entity))
        {
            State.Status = MovementStatus.Success;
            return State;
        }

        var dy = Destination.Y - Source.Y;
        State.Status = MovementStatus.Running;
        MoveTowards(entity);
        
        // Baritone line 280: Sneak on magma blocks to avoid damage
        var feet = GetFeetPosition(entity);
        var blockBelow = level.GetBlockAt(feet.X, feet.Y - 1, feet.Z);
        if (blockBelow?.Name == "minecraft:magma_block")
        {
            State.Sneak = true;
        }
        else
        {
            State.Sprint = true;
        }

        if (dy > 0 && entity.IsOnGround)
        {
            // Baritone-style jump timing
            var flatDist = Math.Sqrt(Math.Pow(entity.Position.X - (Destination.X + 0.5), 2) + Math.Pow(entity.Position.Z - (Destination.Z + 0.5), 2));
            if (flatDist <= 1.2)
            {
                State.Jump = true;
            }
        }

        _ticksWithoutProgress++;
        if (_ticksWithoutProgress > 100)
        {
            State.Status = MovementStatus.Failed;
        }

        return State;
    }

    public override void Reset()
    {
        base.Reset();
        _ticksWithoutProgress = 0;
    }
}
