using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using Serilog;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for parkour jumps (jumping over gaps).
/// Based on Baritone's MovementParkour.java.
/// </summary>
public class MovementParkour(int srcX, int srcY, int srcZ, int destX, int destY, int destZ, MoveDirection direction, int jumpDistance)
    : MovementBase(srcX, srcY, srcZ, destX, destY, destZ, direction)
{
    /// <summary>
    /// The horizontal distance of the jump (1-4 blocks).
    /// </summary>
    public int JumpDistance { get; } = jumpDistance;

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
        // And check for obstructions in the jump path (Baritone MovementParkour.java parity)
        var dx = Math.Sign(destX - srcX);
        var dz = Math.Sign(destZ - srcZ);
        for (var i = 1; i < JumpDistance; i++)
        {
            var gapX = srcX + dx * i;
            var gapZ = srcZ + dz * i;
            
            // Check for obstructions in the air (hitting a wall mid-jump)
            var gapBody = context.GetBlockState(gapX, srcY, gapZ);
            var gapHead = context.GetBlockState(gapX, srcY + 1, gapZ);
            
            // Temporary Debug Logging
            Log.Debug("[ParkourCheck] Dist: {JumpDistance} i:{I} GapPos: ({GapX}, {SrcY}, {GapZ}) Body: {GapBodyName} Head: {GapHeadName}", JumpDistance, i, gapX, srcY, gapZ, gapBody?.Name, gapHead?.Name);

            if (!MovementHelper.CanWalkThrough(gapBody) || !MovementHelper.CanWalkThrough(gapHead))
            {
                 Log.Debug("[ParkourCheck] BLOCKED at {GapX}, {SrcY}, {GapZ}", gapX, srcY, gapZ);
                 return ActionCosts.CostInf;
            }

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

    /// <summary>
    /// Update movement state each tick.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java:252-308
    /// Note: Java Baritone's super.updateState() sets status from WAITING to RUNNING (Movement.java:232-234)
    /// </summary>
    public override MovementState UpdateState(Entity entity, Level level)
    {
        // Java Baritone's super.updateState() sets WAITING -> RUNNING (Movement.java:232-234)
        // Since we don't have a base UpdateState, we handle it here
        if (State.Status == MovementStatus.Waiting)
        {
            State.Status = MovementStatus.Running;
        }

        // Baritone line 254-256: Return early if not running (after setting Waiting -> Running above)
        if (State.Status != MovementStatus.Running)
        {
            return State;
        }

        var feet = GetFeetPosition(entity);

        // Baritone lines 257-261: Check if fallen below source
        if (feet.Y < Source.Y)
        {
            State.ClearInputs();
            State.Status = MovementStatus.Unreachable;
            return State;
        }

        // Baritone lines 262-264: Sprint if dist >= 4 or ascending
        bool ascend = Destination.Y > Source.Y;
        if (JumpDistance >= 4 || ascend)
        {
            State.Sprint = true;
        }

        // Baritone line 265: Move towards destination
        MoveTowards(entity);

        // Baritone lines 266-275: Check if reached destination
        if (feet.X == Destination.X && feet.Y == Destination.Y && feet.Z == Destination.Z)
        {
            // Note: Java Baritone checks for vines/ladders, but we skip that for now
            // Baritone line 273: Check for lilypads (player.y - feet.y < 0.094)
            if (entity.Position.Y - feet.Y < 0.094)
            {
                State.Status = MovementStatus.Success;
                return State;
            }
        }
        else if (feet.X != Source.X || feet.Y != Source.Y || feet.Z != Source.Z)
        {
            // Baritone lines 277-305: Handle jump logic and overshoot prevention
            
            // Check if we're at src.relative(direction) or airborne
            var srcAdjX = Source.X + Direction.XOffset;
            var srcAdjZ = Source.Z + Direction.ZOffset;
            bool atSourceAdjacent = feet.X == srcAdjX && feet.Y == Source.Y && feet.Z == srcAdjZ;
            bool airborne = entity.Position.Y - Source.Y > 0.0001;

            if (atSourceAdjacent || airborne)
            {
                // Baritone lines 288-295: Prevent jumping too late for 2-block gaps (dist == 3)
                if (JumpDistance == 3 && !ascend)
                {
                    double xDiff = (Source.X + 0.5) - entity.Position.X;
                    double zDiff = (Source.Z + 0.5) - entity.Position.Z;
                    double distFromStart = Math.Max(Math.Abs(xDiff), Math.Abs(zDiff));
                    if (distFromStart < 0.7)
                    {
                        return State;
                    }
                }

                // Baritone line 297: Jump
                State.Jump = true;
            }
            else
            {
                // Baritone lines 298-305: Overshoot handling - stop sprinting and move back
                var destBackX = Destination.X - Direction.XOffset;
                var destBackZ = Destination.Z - Direction.ZOffset;
                bool atDestBack = feet.X == destBackX && feet.Y == Destination.Y && feet.Z == destBackZ;
                
                if (!atDestBack)
                {
                    State.Sprint = false;
                    var srcBackX = Source.X - Direction.XOffset;
                    var srcBackZ = Source.Z - Direction.ZOffset;
                    
                    if (feet.X == srcBackX && feet.Y == Source.Y && feet.Z == srcBackZ)
                    {
                        // Move towards source
                        var targetX = Source.X + 0.5;
                        var targetZ = Source.Z + 0.5;
                        var yaw = MovementHelper.CalculateYaw(entity.Position.X, entity.Position.Z, targetX, targetZ);
                        State.SetTarget(yaw, 0);
                        State.MoveForward = true;
                    }
                    else
                    {
                        // Move towards src.relative(direction, -1)
                        var targetX = srcBackX + 0.5;
                        var targetZ = srcBackZ + 0.5;
                        var yaw = MovementHelper.CalculateYaw(entity.Position.X, entity.Position.Z, targetX, targetZ);
                        State.SetTarget(yaw, 0);
                        State.MoveForward = true;
                    }
                }
            }
        }

        return State;
    }

    public override void Reset()
    {
        base.Reset();
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
