using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for digging straight down.
/// Based on Baritone's MovementDownward.java.
/// </summary>
public class MovementDownward : MovementBase
{
    public MovementDownward(int srcX, int srcY, int srcZ)
        : base(srcX, srcY, srcZ, srcX, srcY - 1, srcZ, MoveDirection.Downward)
    {
    }

    public override double CalculateCost(CalculationContext context)
    {
        var x = Source.X;
        var y = Source.Y;
        var z = Source.Z;

        if (!context.AllowDownward || !context.AllowBreak)
        {
            return ActionCosts.CostInf;
        }

        // Check the block we need to break
        var toBreak = context.GetBlockState(x, y - 1, z);
        if (toBreak == null)
        {
            return ActionCosts.CostInf;
        }

        // Can't break bedrock or similar
        if (toBreak.Name.Contains("bedrock", StringComparison.OrdinalIgnoreCase))
        {
            return ActionCosts.CostInf;
        }

        // Check there's something to land on
        var floor = context.GetBlockState(x, y - 2, z);
        if (!MovementHelper.CanWalkOn(floor))
        {
            // Check for multi-block fall (up to maxFallHeight)
            var fallDist = 1;
            for (var i = 2; i <= context.MaxFallHeightNoWater + 1; i++)
            {
                var checkFloor = context.GetBlockState(x, y - 1 - i, z);
                if (MovementHelper.CanWalkOn(checkFloor))
                {
                    fallDist = i;
                    break;
                }
                if (MovementHelper.IsWater(checkFloor))
                {
                    // Water breaks fall
                    fallDist = i;
                    break;
                }
            }
            if (fallDist > context.MaxFallHeightNoWater && !context.HasWaterBucket)
            {
                return ActionCosts.CostInf;
            }
        }

        // TODO: Calculate actual block break time based on tool and block
        var breakCost = ActionCosts.WalkOneBlockCost * 4; // Placeholder

        Cost = breakCost + ActionCosts.GetFallCost(1);
        return Cost;
    }

    public override MovementState UpdateState(Entity entity, Level level)
    {
        var feet = GetFeetPosition(entity);
        var blockBelow = level.GetBlockAt(Source.X, Source.Y - 1, Source.Z);
        bool ladder = MovementHelper.IsClimbable(blockBelow);

        // Check if we've arrived
        if (feet.Y <= Destination.Y && (entity.IsOnGround || ladder))
        {
            State.ClearInputs();
            State.Status = MovementStatus.Success;
            return State;
        }

        State.Status = MovementStatus.Running;

        if (ladder)
        {
            // Back away from ladder to descend
            State.MoveBackward = true;
            // Or look at the ladder and sneak?
            // Baritone usually just backs away or sneaks.
        }
        else
        {
            // Look down and break the block below
            State.SetTarget(entity.YawPitch.X, 90); // Look straight down
            State.LeftClick = true; // Break block
        }

        return State;
    }
}
