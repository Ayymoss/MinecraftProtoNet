using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;
using Microsoft.Extensions.Logging;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for walking one block horizontally.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementTraverse.java
/// </summary>
public class MovementTraverse : MovementBase
{
    // Baritone line 55
    private bool _wasTheBridgeBlockAlwaysThere = true;

    public MovementTraverse(int srcX, int srcY, int srcZ, int destX, int destZ, MoveDirection direction)
        : base(srcX, srcY, srcZ, destX, srcY, destZ, direction)
    {
    }

    /// <summary>
    /// Calculate cost for this movement.
    /// Reference: Baritone lines 77-169 - cost() method
    /// </summary>
    public override double CalculateCost(CalculationContext context)
    {
        var destX = Destination.X;
        var destZ = Destination.Z;
        var x = Source.X;
        var y = Source.Y;
        var z = Source.Z;

        // Baritone lines 78-81: Get destination blocks
        var pb0 = context.GetBlockState(destX, y + 1, destZ); // head
        var pb1 = context.GetBlockState(destX, y, destZ);     // body
        var destOn = context.GetBlockState(destX, y - 1, destZ); // floor
        var srcDown = context.GetBlockState(x, y - 1, z);

        // Baritone line 83
        bool standingOnABlock = MovementHelper.CanWalkOn(srcDown);

        // Baritone lines 85-121: Walk case (floor exists)
        if (MovementHelper.CanWalkOn(destOn))
        {
            double WC = ActionCosts.WalkOneBlockCost;
            bool water = false;

            // Baritone lines 88-102: Check water/soul sand penalties
            if (MovementHelper.IsWater(pb0) || MovementHelper.IsWater(pb1))
            {
                WC = ActionCosts.WalkOneInWaterCost;
                water = true;
            }
            else
            {
                // Baritone lines 92-101: Soul sand penalties
                if (MovementHelper.IsSoulSand(destOn))
                {
                    WC += (ActionCosts.WalkOneOverSoulSandCost - ActionCosts.WalkOneBlockCost) / 2;
                }
                if (MovementHelper.IsSoulSand(srcDown))
                {
                    WC += (ActionCosts.WalkOneOverSoulSandCost - ActionCosts.WalkOneBlockCost) / 2;
                }
            }

            // Baritone lines 103-106: Mining cost for body
            double hardness1 = MovementHelper.GetMiningDurationTicks(context, destX, y, destZ, false);
            if (hardness1 >= ActionCosts.CostInf)
            {
                return ActionCosts.CostInf;
            }

            // Baritone lines 107-120: Mining cost for head and sprint check
            double hardness2 = MovementHelper.GetMiningDurationTicks(context, destX, y + 1, destZ, true);
            if (hardness1 == 0 && hardness2 == 0)
            {
                // Baritone lines 109-115: Can sprint if nothing to break
                if (!water && context.CanSprint)
                {
                    WC *= ActionCosts.SprintMultiplier;
                }
                Cost = WC;
                return Cost;
            }

            // Baritone lines 117-120: Ladder/vine penalty
            if (MovementHelper.IsClimbable(srcDown))
            {
                hardness1 *= 5;
                hardness2 *= 5;
            }

            Cost = WC + hardness1 + hardness2;
            return Cost;
        }
        else
        {
            // Baritone lines 122-168: Bridge case (need to place)
            
            // Baritone lines 123-125: Can't bridge from ladder/vine
            if (MovementHelper.IsClimbable(srcDown))
            {
                context.Logger?.LogDebug("[MovementTraverse] CostInf: Cannot bridge from climbable block at ({X}, {Y}, {Z}). Block: {BlockName}",
                    x, y - 1, z, srcDown?.Name ?? "null");
                return ActionCosts.CostInf;
            }

            // Baritone line 126: Check if replaceable
            if (!MovementHelper.IsReplaceable(destOn))
            {
                context.Logger?.LogDebug("[MovementTraverse] CostInf: Destination floor at ({X}, {Y}, {Z}) is not replaceable. Block: {BlockName}",
                    destX, y - 1, destZ, destOn?.Name ?? "null");
                return ActionCosts.CostInf;
            }

            // Baritone lines 127-131: Check water conditions
            bool throughWater = MovementHelper.IsWater(pb0) || MovementHelper.IsWater(pb1);
            if (MovementHelper.IsWater(destOn) && throughWater)
            {
                context.Logger?.LogDebug("[MovementTraverse] CostInf: Cannot traverse in water. destOn is water and throughWater=true");
                return ActionCosts.CostInf;
            }

            // Baritone line 132-135: Get place cost
            double placeCost = context.CostOfPlacingAt(destX, y - 1, destZ);
            if (placeCost >= ActionCosts.CostInf)
            {
                context.Logger?.LogDebug("[MovementTraverse] CostInf: Cannot place block at ({X}, {Y}, {Z}) - CostOfPlacingAt returned CostInf. HasThrowaway={HasThrowaway}, AllowPlace={AllowPlace}",
                    destX, y - 1, destZ, context.HasThrowaway, context.AllowPlace);
                return ActionCosts.CostInf;
            }

            // Baritone lines 136-140: Mining costs
            double hardness1 = MovementHelper.GetMiningDurationTicks(context, destX, y, destZ, false);
            if (hardness1 >= ActionCosts.CostInf)
            {
                context.Logger?.LogDebug("[MovementTraverse] CostInf: Mining cost for body block at ({X}, {Y}, {Z}) is CostInf. Cost: {Cost}",
                    destX, y, destZ, hardness1);
                return ActionCosts.CostInf;
            }
            double hardness2 = MovementHelper.GetMiningDurationTicks(context, destX, y + 1, destZ, true);

            double WC = throughWater ? ActionCosts.WalkOneInWaterCost : ActionCosts.WalkOneBlockCost;

            // Baritone lines 142-152: Check for side-place options
            int[][] directions = [[0, 0, -1], [0, 0, 1], [1, 0, 0], [-1, 0, 0], [0, -1, 0]];
            string[] directionNames = ["North", "South", "East", "West", "Down"];
            bool foundSidePlace = false;
            for (int i = 0; i < 5; i++)
            {
                int againstX = destX + directions[i][0];
                int againstY = y - 1 + directions[i][1];
                int againstZ = destZ + directions[i][2];
                if (againstX == x && againstZ == z)
                {
                    context.Logger?.LogDebug("[MovementTraverse] Skipping {Direction} adjacent block at ({X}, {Y}, {Z}) - it's the source position (backplace)",
                        directionNames[i], againstX, againstY, againstZ);
                    continue; // Skip backplace for now
                }
                var againstBlock = context.GetBlockState(againstX, againstY, againstZ);
                bool canPlace = MovementHelper.CanPlaceAgainst(againstBlock);
                context.Logger?.LogDebug("[MovementTraverse] Checking {Direction} adjacent block at ({X}, {Y}, {Z}): {BlockName}, CanPlaceAgainst={CanPlace}",
                    directionNames[i], againstX, againstY, againstZ, againstBlock?.Name ?? "null", canPlace);
                if (canPlace)
                {
                    context.Logger?.LogDebug("[MovementTraverse] Found side place option: {Direction} block at ({X}, {Y}, {Z})",
                        directionNames[i], againstX, againstY, againstZ);
                    Cost = WC + placeCost + hardness1 + hardness2;
                    return Cost;
                }
            }

            // Baritone lines 154-165: Backplace case
            context.Logger?.LogDebug("[MovementTraverse] No side place option found, checking backplace. standingOnABlock={StandingOn}, srcDown={SrcDown}",
                standingOnABlock, srcDown?.Name ?? "null");
            if (MovementHelper.IsSoulSand(srcDown) || MovementHelper.IsBottomSlab(srcDown))
            {
                context.Logger?.LogDebug("[MovementTraverse] CostInf: Cannot backplace against soul sand or slab. srcDown: {BlockName}",
                    srcDown?.Name ?? "null");
                return ActionCosts.CostInf; // Can't backplace against soul sand/slabs
            }
            if (!standingOnABlock)
            {
                context.Logger?.LogDebug("[MovementTraverse] CostInf: Cannot backplace - not standing on a block");
                return ActionCosts.CostInf;
            }
            // Baritone lines 161-163: Lily pad/carpet check
            var blockSrc = context.GetBlockState(x, y, z);
            if ((MovementHelper.IsLilyPad(blockSrc) || MovementHelper.IsCarpet(blockSrc)) && 
                MovementHelper.IsLiquid(srcDown))
            {
                context.Logger?.LogDebug("[MovementTraverse] CostInf: Cannot backplace - standing on lily pad/carpet over liquid");
                return ActionCosts.CostInf;
            }

            // Baritone line 164: Sneak backplace cost
            WC = WC * (ActionCosts.SneakOneBlockCost / ActionCosts.WalkOneBlockCost);
            Cost = WC + placeCost + hardness1 + hardness2;
            return Cost;
        }
    }

    /// <summary>
    /// Get blocks that need to be broken for this movement.
    /// Reference: Baritone constructor line 58
    /// </summary>
    public override IEnumerable<(int X, int Y, int Z)> GetBlocksToBreak(CalculationContext context)
    {
        var destX = Destination.X;
        var destY = Destination.Y;
        var destZ = Destination.Z;

        // Baritone line 58: new BetterBlockPos[]{to.above(), to}
        var destHead = context.GetBlockState(destX, destY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(destHead))
            yield return (destX, destY + 1, destZ);

        var destBody = context.GetBlockState(destX, destY, destZ);
        if (!MovementHelper.CanWalkThrough(destBody))
            yield return (destX, destY, destZ);
    }

    /// <summary>
    /// Get blocks that need to be placed for this movement.
    /// Reference: Baritone constructor line 58 - positionToPlace = to.below()
    /// </summary>
    public override IEnumerable<(int X, int Y, int Z)> GetBlocksToPlace(CalculationContext context)
    {
        var destX = Destination.X;
        var srcY = Source.Y;
        var destZ = Destination.Z;

        var destFloor = context.GetBlockState(destX, srcY - 1, destZ);
        if (!MovementHelper.CanWalkOn(destFloor) && MovementHelper.IsReplaceable(destFloor))
        {
            yield return (destX, srcY - 1, destZ);
        }
    }

    /// <summary>
    /// Update movement state each tick.
    /// Reference: Baritone lines 172-358 - updateState() method
    /// </summary>
    public override MovementState UpdateState(Entity entity, Level level)
    {
        var feet = GetFeetPosition(entity);

        // Baritone lines 176-214: Handle PREPPING state (block breaking)
        // Not implemented - handled by caller

        // Baritone line 217
        State.Sneak = false;

        // Baritone lines 219-220
        var srcDown = level.GetBlockAt(Source.X, Source.Y - 1, Source.Z);
        bool ladder = MovementHelper.IsClimbable(srcDown);

        // Baritone lines 244-253: Check Y level
        if (feet.Y != Destination.Y && !ladder)
        {
            if (feet.Y < Destination.Y)
            {
                State.Jump = true;
            }
            State.Status = MovementStatus.Running;
            MoveTowards(entity);
            return State;
        }

        // Baritone line 244: Check if bridge block exists
        var destFloor = level.GetBlockAt(Destination.X, Destination.Y - 1, Destination.Z);
        bool isTheBridgeBlockThere = MovementHelper.CanWalkOn(destFloor) || ladder;

        if (isTheBridgeBlockThere)
        {
            // Baritone lines 256-258: Success check
            if (feet.X == Destination.X && feet.Y == Destination.Y && feet.Z == Destination.Z)
            {
                State.Status = MovementStatus.Success;
                State.ClearInputs();
                return State;
            }

            // Baritone lines 262-268: Ladder wait logic
            var srcBody = level.GetBlockAt(Source.X, Source.Y, Source.Z);
            var srcHead = level.GetBlockAt(Source.X, Source.Y + 1, Source.Z);
            if (entity.Position.Y > Source.Y + 0.1 && !entity.IsOnGround &&
                (MovementHelper.IsClimbable(srcBody) || MovementHelper.IsClimbable(srcHead)))
            {
                // Wait until on ground when leaving ladder
                State.Status = MovementStatus.Running;
                return State;
            }

            // Baritone lines 272-274: Sprint check
            if (_wasTheBridgeBlockAlwaysThere && !entity.IsInWater)
            {
                var intoX = Destination.X + (Destination.X - Source.X);
                var intoZ = Destination.Z + (Destination.Z - Source.Z);
                var intoBelow = level.GetBlockAt(intoX, Destination.Y, intoZ);
                var intoAbove = level.GetBlockAt(intoX, Destination.Y + 1, intoZ);

                if ((intoBelow == null || !MovementHelper.AvoidWalkingInto(intoBelow) || MovementHelper.IsWater(intoBelow)) &&
                    (intoAbove == null || !MovementHelper.AvoidWalkingInto(intoAbove)))
                {
                    State.Sprint = true;
                }
            }

            MoveTowards(entity);
        }
        else
        {
            // Baritone line 288
            _wasTheBridgeBlockAlwaysThere = false;

            // Baritone lines 290-297: Soul sand/slab backplace issue
            var standingOn = level.GetBlockAt(feet.X, feet.Y - 1, feet.Z);
            if (MovementHelper.IsSoulSand(standingOn) || MovementHelper.IsBottomSlab(standingOn))
            {
                double dist = Math.Max(
                    Math.Abs(Destination.X + 0.5 - entity.Position.X),
                    Math.Abs(Destination.Z + 0.5 - entity.Position.Z));
                if (dist < 0.85)
                {
                    MoveTowards(entity);
                    State.MoveForward = false;
                    State.MoveBackward = true;
                    State.Status = MovementStatus.Running;
                    return State;
                }
            }

            // Baritone lines 298-302: Sneak while placing
            double dist1 = Math.Max(
                Math.Abs(entity.Position.X - (Destination.X + 0.5)),
                Math.Abs(entity.Position.Z - (Destination.Z + 0.5)));
            if (dist1 < 0.6)
            {
                State.Sneak = true;
            }

            // Set placement target
            State.RightClick = true;
            State.PlaceBlockTarget = (Destination.X, Destination.Y - 1, Destination.Z);

            MoveTowards(entity);
        }

        State.Status = MovementStatus.Running;
        return State;
    }

    /// <summary>
    /// Reset movement state.
    /// Reference: Baritone lines 62-65 - reset() method
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        _wasTheBridgeBlockAlwaysThere = true;
    }

    /// <summary>
    /// Check if safe to cancel this movement.
    /// Reference: Baritone lines 362-367 - safeToCancel() method
    /// </summary>
    public override bool SafeToCancel()
    {
        // Safe if not running, or if the bridge block exists (wasn't placed)
        return State.Status != MovementStatus.Running || _wasTheBridgeBlockAlwaysThere;
    }
}
