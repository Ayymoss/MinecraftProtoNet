using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for parkour jumps (jumping over gaps).
/// Based on Baritone's MovementParkour.java.
/// </summary>
public class MovementParkour : MovementBase
{
    /// <summary>
    /// The horizontal distance of the jump (1-4 blocks).
    /// </summary>
    public int JumpDistance { get; }

    private int _ticksWithoutProgress;

    public MovementParkour(int srcX, int srcY, int srcZ, int destX, int destY, int destZ, MoveDirection direction, int jumpDistance)
        : base(srcX, srcY, srcZ, destX, destY, destZ, direction)
    {
        JumpDistance = jumpDistance;
    }

    public override double CalculateCost(CalculationContext context)
    {
        if (!context.AllowParkour)
        {
            return ActionCosts.CostInf;
        }

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

        // Check destination clearance
        var destBody = context.GetBlockState(destX, destY, destZ);
        var destHead = context.GetBlockState(destX, destY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(destBody) || !MovementHelper.CanWalkThrough(destHead))
        {
            return ActionCosts.CostInf;
        }

        // Check jump clearance (head space during jump arc)
        var jumpSpace = context.GetBlockState(srcX, srcY + 2, srcZ);
        if (!MovementHelper.CanWalkThrough(jumpSpace))
        {
            return ActionCosts.CostInf;
        }

        // Check that there's a gap (we need to actually jump over something)
        var dx = Math.Sign(destX - srcX);
        var dz = Math.Sign(destZ - srcZ);
        for (var i = 1; i < JumpDistance; i++)
        {
            var gapX = srcX + dx * i;
            var gapZ = srcZ + dz * i;
            var gapFloor = context.GetBlockState(gapX, srcY - 1, gapZ);
            
            // For a valid parkour, intermediate blocks should be gaps (no floor)
            if (MovementHelper.CanWalkOn(gapFloor))
            {
                // There's floor here, not a proper parkour
                // Could still be valid if it's 1-block higher (ascending parkour)
                if (JumpDistance == 1)
                {
                    return ActionCosts.CostInf;
                }
            }
        }

        // Calculate cost based on distance
        // Sprint jumping covers ~4 blocks, so longer jumps are more costly
        var baseCost = ActionCosts.SprintOneBlockCost * JumpDistance;
        var jumpCost = ActionCosts.JumpOneBlockCost;
        
        // Penalty for longer jumps (risk factor)
        var riskPenalty = JumpDistance > 2 ? (JumpDistance - 2) * ActionCosts.WalkOneBlockCost : 0;

        Cost = baseCost + jumpCost + riskPenalty + context.JumpPenalty;
        return Cost;
    }

    public override MovementState UpdateState(Entity entity, Level level)
    {
        var feet = GetFeetPosition(entity);

        // Check if we've arrived
        if (feet.X == Destination.X && feet.Y == Destination.Y && feet.Z == Destination.Z && entity.IsOnGround)
        {
            State.Status = MovementStatus.Success;
            return State;
        }

        // Check for failure (fell into the gap)
        if (entity.IsOnGround && feet.Y < Source.Y)
        {
            State.ClearInputs();
            State.Status = MovementStatus.Failed;
            return State;
        }

        State.Status = MovementStatus.Running;

        // Need to sprint for distance
        State.Sprint = true;
        MoveTowards(entity);

        // Calculate when to jump
        var dx = Math.Abs(entity.Position.X - (Source.X + 0.5));
        var dz = Math.Abs(entity.Position.Z - (Source.Z + 0.5));
        var distFromSource = Math.Max(dx, dz);

        // Jump when near the edge
        if (entity.IsOnGround && distFromSource > 0.6)
        {
            State.Jump = true;
        }

        _ticksWithoutProgress++;
        if (_ticksWithoutProgress > 80)
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

    /// <summary>
    /// Creates parkour movements for all valid jump distances (1-4 blocks).
    /// </summary>
    public static IEnumerable<MovementParkour> CreateParkourMoves(CalculationContext context, int srcX, int srcY, int srcZ, MoveDirection direction)
    {
        var dx = direction.XOffset / Math.Max(1, Math.Abs(direction.XOffset));
        var dz = direction.ZOffset / Math.Max(1, Math.Abs(direction.ZOffset));

        if (dx == 0 && dz == 0) yield break;

        // Try jump distances 2-4 (1 is just a regular ascend)
        for (var dist = 2; dist <= 4; dist++)
        {
            var destX = srcX + dx * dist;
            var destZ = srcZ + dz * dist;
            
            var move = new MovementParkour(srcX, srcY, srcZ, destX, srcY, destZ, direction, dist);
            var cost = move.CalculateCost(context);
            
            if (cost < ActionCosts.CostInf)
            {
                yield return move;
            }
        }
    }
}
