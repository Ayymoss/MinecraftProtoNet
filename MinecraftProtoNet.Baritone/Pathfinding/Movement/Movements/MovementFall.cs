using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for falling multiple blocks with optional water bucket.
/// Based on Baritone's MovementFall.java.
/// </summary>
public class MovementFall : MovementBase
{
    /// <summary>
    /// The actual fall distance (can be more than 1 block).
    /// </summary>
    public int FallDistance { get; }

    public MovementFall(int srcX, int srcY, int srcZ, int destX, int destY, int destZ, MoveDirection direction)
        : base(srcX, srcY, srcZ, destX, destY, destZ, direction)
    {
        FallDistance = srcY - destY;
    }

    public override double CalculateCost(CalculationContext context)
    {
        var destX = Destination.X;
        var destY = Destination.Y;
        var destZ = Destination.Z;
        var srcY = Source.Y;

        var fallHeight = srcY - destY;

        // Check fall height limits
        if (fallHeight > context.MaxFallHeightNoWater)
        {
            if (!context.HasWaterBucket || fallHeight > context.MaxFallHeightBucket)
            {
                return ActionCosts.CostInf;
            }
            // Water bucket save - add extra cost for placing water
            Cost = ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(fallHeight) +
                   ActionCosts.CenterAfterFallCost + ActionCosts.WalkOneBlockCost * 2;
            return Cost;
        }

        // Check destination floor
        var destFloor = context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor) && !MovementHelper.IsWater(destFloor))
        {
            return ActionCosts.CostInf;
        }

        // Check clearance during fall
        for (var y = srcY; y > destY; y--)
        {
            var bodyBlock = context.GetBlockState(destX, y, destZ);
            if (!MovementHelper.CanWalkThrough(bodyBlock))
            {
                return ActionCosts.CostInf;
            }
        }

        // Check destination clearance
        var destBody = context.GetBlockState(destX, destY, destZ);
        var destHead = context.GetBlockState(destX, destY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(destBody) || !MovementHelper.CanWalkThrough(destHead))
        {
            return ActionCosts.CostInf;
        }

        // Water landing reduces cost (no fall damage)
        if (MovementHelper.IsWater(destBody))
        {
            Cost = ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(fallHeight) * 0.5;
            return Cost;
        }

        Cost = ActionCosts.WalkOffBlockCost + ActionCosts.GetFallCost(fallHeight) + ActionCosts.CenterAfterFallCost;
        return Cost;
    }

    public override MovementState UpdateState(Entity entity, Level level)
    {
        var feet = GetFeetPosition(entity);

        // Check if we've arrived
        if (feet.Y <= Destination.Y && entity.IsOnGround)
        {
            State.Status = MovementStatus.Success;
            return State;
        }

        // Check if landed in water
        if (entity.IsInWater && feet.Y <= Destination.Y + 1)
        {
            State.Status = MovementStatus.Success;
            return State;
        }

        // Baritone line 98-99: Sneak on magma blocks
        var blockBelow = level.GetBlockAt(feet.X, feet.Y - 1, feet.Z);
        if (blockBelow?.Name == "minecraft:magma_block")
        {
            State.Sneak = true;
        }

        // Baritone lines 142-145: Sneak while falling with high velocity and off-center
        // This helps steer towards the destination
        var destCenter = (Destination.X + 0.5, Destination.Z + 0.5);
        var predictedX = entity.Position.X + entity.Velocity.X;
        var predictedZ = entity.Position.Z + entity.Velocity.Z;
        var horizontalOffset = Math.Abs(predictedX - destCenter.Item1) + Math.Abs(predictedZ - destCenter.Item2);
        
        if (!entity.IsOnGround && Math.Abs(entity.Velocity.Y) > 0.4 && horizontalOffset > 0.1)
        {
            State.Sneak = true;
        }

        State.Status = MovementStatus.Running;
        MoveTowards(entity);

        return State;
    }
}
