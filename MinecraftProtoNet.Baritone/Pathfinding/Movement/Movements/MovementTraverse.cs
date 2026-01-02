using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for walking one block horizontally.
/// Based on Baritone's MovementTraverse.java.
/// </summary>
public class MovementTraverse : MovementBase
{
    public MovementTraverse(int srcX, int srcY, int srcZ, int destX, int destZ, MoveDirection direction)
        : base(srcX, srcY, srcZ, destX, srcY, destZ, direction)
    {
    }

    public override double CalculateCost(CalculationContext context)
    {
        var destX = Destination.X;
        var destZ = Destination.Z;
        var srcX = Source.X;
        var srcY = Source.Y;
        var srcZ = Source.Z;

        // Check destination floor
        var destFloor = context.GetBlockState(destX, srcY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor))
        {
            // Check if we can bridge
            if (context.HasThrowaway && MovementHelper.IsReplaceable(destFloor))
            {
                // Bridging cost
                Cost = ActionCosts.SneakOneBlockCost + context.PlaceBlockCost;
                return Cost;
            }
            return ActionCosts.CostInf;
        }

        // Check body and head clearance
        var destBody = context.GetBlockState(destX, srcY, destZ);
        var destHead = context.GetBlockState(destX, srcY + 1, destZ);

        if (!MovementHelper.CanWalkThrough(destBody) || !MovementHelper.CanWalkThrough(destHead))
        {
            // Would need to break blocks
            if (!context.AllowBreak)
            {
                return ActionCosts.CostInf;
            }
            
            double totalCost = ActionCosts.WalkOneBlockCost; // Base movement

            // Calc break cost for body
            if (destBody != null && !MovementHelper.CanWalkThrough(destBody))
            {
                float speed = context.GetBestToolSpeed?.Invoke(destBody) ?? 1.0f;
                // Treat as harvestable for now (assume we have tools or hand is fine)
                double breakCost = ActionCosts.CalculateMiningDuration(speed, destBody.DestroySpeed, true);
                if (breakCost >= ActionCosts.CostInf) return ActionCosts.CostInf;
                totalCost += breakCost;
            }

            // Calc break cost for head
            if (destHead != null && !MovementHelper.CanWalkThrough(destHead))
            {
                float speed = context.GetBestToolSpeed?.Invoke(destHead) ?? 1.0f;
                double breakCost = ActionCosts.CalculateMiningDuration(speed, destHead.DestroySpeed, true);
                if (breakCost >= ActionCosts.CostInf) return ActionCosts.CostInf;
                totalCost += breakCost;
            }

            Cost = totalCost + context.BreakBlockAdditionalCost; // Add penalty
            return Cost;
        }

        // Check for special blocks
        var floorBlock = context.GetBlockState(srcX, srcY - 1, srcZ);
        var cost = ActionCosts.WalkOneBlockCost;

        // Soul sand slows down
        if (MovementHelper.IsSoulSand(destFloor) || MovementHelper.IsSoulSand(floorBlock))
        {
            cost += (ActionCosts.WalkOneOverSoulSandCost - ActionCosts.WalkOneBlockCost) / 2;
        }

        // Water slows down
        if (MovementHelper.IsWater(destBody) || MovementHelper.IsWater(destHead))
        {
            cost = ActionCosts.WalkOneInWaterCost;
        }
        else if (context.CanSprint && !MovementHelper.IsSoulSand(floorBlock))
        {
            // Can sprint if not in water and not on soul sand
            cost *= ActionCosts.SprintMultiplier;
        }

        Cost = cost;
        return Cost;
    }

    private bool _wasTheBridgeBlockAlwaysThere = true;
    private int _ticksWithoutProgress;

    public override MovementState UpdateState(Entity entity, Level level)
    {
        // Check if we've arrived
        if (HasReachedDestination(entity))
        {
            State.Status = MovementStatus.Success;
            State.BreakBlockTarget = null; // Clear any break target
            return State;
        }

        // Check Y-level mismatch (like Baritone lines 250-257)
        var feetY = (int)Math.Floor(entity.Position.Y);
        if (feetY != Destination.Y)
        {
            // Wrong Y coordinate - probably need to jump up
            if (feetY < Destination.Y)
            {
                State.Jump = true;
            }
            State.Status = MovementStatus.Running;
            MoveTowards(entity);
            return State;
        }
        
        // Check for obstructions to break
        var destBody = level.GetBlockAt(Destination.X, Destination.Y, Destination.Z);
        if (destBody != null && !MovementHelper.CanWalkThrough(destBody))
        {
            State.Status = MovementStatus.Running;
            State.ClearInputs();
            State.BreakBlockTarget = (Destination.X, Destination.Y, Destination.Z);
            return State;
        }

        var destHead = level.GetBlockAt(Destination.X, Destination.Y + 1, Destination.Z);
        if (destHead != null && !MovementHelper.CanWalkThrough(destHead))
        {
            State.Status = MovementStatus.Running;
            State.ClearInputs();
            State.BreakBlockTarget = (Destination.X, Destination.Y + 1, Destination.Z);
            return State;
        }

        // Clear any previous break target if clear
        State.BreakBlockTarget = null;

        // Check if the bridge block is there or we need to place
        var destFloor = level.GetBlockAt(Destination.X, Destination.Y - 1, Destination.Z);
        bool isTheBridgeBlockThere = MovementHelper.CanWalkOn(destFloor);

        if (isTheBridgeBlockThere)
        {
            // Normal traversal - conditionally sprint
            // Only sprint if bridge was always there (not placed) and not in liquid
            // See Baritone lines 276-278
            if (_wasTheBridgeBlockAlwaysThere && !entity.IsInWater && !entity.IsInLava)
            {
                // Check if next block is also safe (avoid walking into hazards)
                var intoX = Destination.X + (Destination.X - Source.X);
                var intoZ = Destination.Z + (Destination.Z - Source.Z);
                var intoBelow = level.GetBlockAt(intoX, Destination.Y, intoZ);
                var intoAbove = level.GetBlockAt(intoX, Destination.Y + 1, intoZ);
                
                bool intoSafe = intoBelow == null || !MovementHelper.AvoidWalkingInto(intoBelow) || MovementHelper.IsWater(intoBelow);
                bool intoAboveSafe = intoAbove == null || !MovementHelper.AvoidWalkingInto(intoAbove);
                
                if (intoSafe && intoAboveSafe)
                {
                    State.Sprint = true;
                }
                else
                {
                    State.Sprint = false;
                }
            }
            else
            {
                State.Sprint = false;
            }
        }
        else
        {
            _wasTheBridgeBlockAlwaysThere = false;
            // Need to place a block (bridging). No sprint while bridging.
            State.Sprint = false;
            State.Sneak = true; // Sneak for safety
        }

        // Still in progress
        State.Status = MovementStatus.Running;
        MoveTowards(entity);

        // Stuck detection with collision awareness
        if (entity.HorizontalCollision)
        {
            _ticksWithoutProgress++;
            if (_ticksWithoutProgress > 40)
            {
                State.Status = MovementStatus.Failed;
            }
        }
        else
        {
            _ticksWithoutProgress = 0;
        }

        return State;
    }
}
