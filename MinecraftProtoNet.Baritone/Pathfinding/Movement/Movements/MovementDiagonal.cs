using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

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

    /// <summary>
    /// Returns valid positions for diagonal movement.
    /// Based on Baritone MovementDiagonal.calculateValidPositions() lines 99-109.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDiagonal.java
    /// </summary>
    public override HashSet<(int X, int Y, int Z)> GetValidPositions()
    {
        var diagA = (Source.X, Source.Y, Destination.Z);
        var diagB = (Destination.X, Source.Y, Source.Z);
        
        if (Destination.Y < Source.Y) // Descending diagonal
        {
            return [Source, (Destination.X, Destination.Y + 1, Destination.Z), diagA, diagB, 
                    Destination, (diagA.X, diagA.Y - 1, diagA.Item3), (diagB.X, diagB.Y - 1, diagB.Item3)];
        }
        if (Destination.Y > Source.Y) // Ascending diagonal
        {
            return [Source, (Source.X, Source.Y + 1, Source.Z), diagA, diagB, 
                    Destination, (diagA.X, diagA.Y + 1, diagA.Item3), (diagB.X, diagB.Y + 1, diagB.Item3)];
        }
        // Same level diagonal
        return [Source, Destination, diagA, diagB];
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

    public override IEnumerable<(int X, int Y, int Z)> GetBlocksToBreak(CalculationContext context)
    {
        var destX = Destination.X;
        var destY = Destination.Y;
        var destZ = Destination.Z;
        var srcX = Source.X;
        var srcY = Source.Y;
        var srcZ = Source.Z;

        var dy = destY - srcY;
        var dx = destX - srcX;
        var dz = destZ - srcZ;

        // Destination body and head
        var destBody = context.GetBlockState(destX, destY, destZ);
        var destHead = context.GetBlockState(destX, destY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(destBody)) yield return (destX, destY, destZ);
        if (!MovementHelper.CanWalkThrough(destHead)) yield return (destX, destY + 1, destZ);

        // Intermediate corner checks
        // We need EITHER corner A (Body+Head) OR corner B (Body+Head) clear
        // If both blocked, we break one set (prefer A for simplicity or closeness)
        
        var cornerABody = context.GetBlockState(srcX + dx, srcY, srcZ);
        var cornerAHead = context.GetBlockState(srcX + dx, srcY + 1, srcZ);
        bool canA = MovementHelper.CanWalkThrough(cornerABody) && MovementHelper.CanWalkThrough(cornerAHead);
        
        var cornerBBody = context.GetBlockState(srcX, srcY, srcZ + dz);
        var cornerBHead = context.GetBlockState(srcX, srcY + 1, srcZ + dz);
        bool canB = MovementHelper.CanWalkThrough(cornerBBody) && MovementHelper.CanWalkThrough(cornerBHead);

        if (!canA && !canB)
        {
            // Both blocked, break A
            if (!MovementHelper.CanWalkThrough(cornerABody)) yield return (srcX + dx, srcY, srcZ);
            if (!MovementHelper.CanWalkThrough(cornerAHead)) yield return (srcX + dx, srcY + 1, srcZ);
        }

        if (dy > 0) // Ascend diagonal
        {
            var srcCeil = context.GetBlockState(srcX, srcY + 2, srcZ);
            if (!MovementHelper.CanWalkThrough(srcCeil)) yield return (srcX, srcY + 2, srcZ);

            var cornerACeil = context.GetBlockState(srcX + dx, srcY + 2, srcZ);
            var cornerBCeil = context.GetBlockState(srcX, srcY + 2, srcZ + dz);
            
            bool canACeil = canA && MovementHelper.CanWalkThrough(cornerACeil);
            bool canBCeil = canB && MovementHelper.CanWalkThrough(cornerBCeil);

            if (!canACeil && !canBCeil)
            {
                // Both paths blocked above, break A path ceiling if we chose A, or just A ceiling
                if (!MovementHelper.CanWalkThrough(cornerACeil)) yield return (srcX + dx, srcY + 2, srcZ);
            }
        }
    }

    public override IEnumerable<(int X, int Y, int Z)> GetBlocksToPlace(CalculationContext context)
    {
        var destX = Destination.X;
        var destY = Destination.Y;
        var destZ = Destination.Z;

        // Check destination floor
        var destFloor = context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor) && context.HasThrowaway && MovementHelper.IsReplaceable(destFloor))
        {
             yield return (destX, destY - 1, destZ);
        }
    }

    public override MovementState UpdateState(Entity entity, Level level)
    {
        if (HasReachedDestination(entity))
        {
            State.Status = MovementStatus.Success;
            State.ClearInputs();
            return State;
        }

        // Clear interaction inputs for the new tick
        State.ClearInputs();
        State.BreakBlockTarget = null;
        State.PlaceBlockTarget = null;

        var dy = Destination.Y - Source.Y;
        State.Status = MovementStatus.Running;
        MoveTowards(entity);

        // Clear placement targets
        State.RightClick = false;
        State.PlaceBlockTarget = null;

        // Check for bridging
        var destFloor = level.GetBlockAt(Destination.X, Destination.Y - 1, Destination.Z);
        if (!MovementHelper.CanWalkOn(destFloor))
        {
            State.Sneak = true;
            State.Sprint = false;
            State.RightClick = true;
            State.PlaceBlockTarget = (Destination.X, Destination.Y - 1, Destination.Z);
        }
        else
        {
            // Normal movement
            State.Sprint = true;
        }

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
