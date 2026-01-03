using MinecraftProtoNet.State;
using Serilog;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;
using Microsoft.Extensions.Logging;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for jumping up one block.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementAscend.java
/// </summary>
public class MovementAscend(int srcX, int srcY, int srcZ, int destX, int destZ, MoveDirection direction)
    : MovementBase(srcX, srcY, srcZ, destX, srcY + 1, destZ, direction)
{
    // Baritone line 39
    private int _ticksWithoutPlacement;

    /// <summary>
    /// Returns valid positions for ascend movement.
    /// Reference: Baritone lines 57-65
    /// </summary>
    public override HashSet<(int X, int Y, int Z)> GetValidPositions()
    {
        // Baritone line 58: BetterBlockPos prior = new BetterBlockPos(src.subtract(getDirection()).above());
        var dx = Destination.X - Source.X;
        var dz = Destination.Z - Source.Z;
        var prior = (Source.X - dx, Source.Y + 1, Source.Z - dz);
        
        // Baritone lines 59-64
        return
        [
            Source,
            (Source.X, Source.Y + 1, Source.Z), // src.above
            Destination,                         // dest
            prior,                               // prior
            (prior.Item1, prior.Item2 + 1, prior.Item3) // prior.above
        ];
    }

    /// <summary>
    /// Calculate cost for this movement.
    /// Reference: Baritone lines 67-155 - cost() method
    /// </summary>
    public override double CalculateCost(CalculationContext context)
    {
        var destX = Destination.X;
        var y = Source.Y; // Baritone uses 'y' for source Y throughout
        var destZ = Destination.Z;
        var x = Source.X;
        var z = Source.Z;

        // Baritone line 68: BlockState toPlace = context.get(destX, y, destZ);
        var toPlace = context.GetBlockState(destX, y, destZ);
        context.Logger?.LogDebug("[MovementAscend] Movement from ({SrcX}, {SrcY}, {SrcZ}) to ({DestX}, {DestY}, {DestZ}). Destination floor at ({FloorX}, {FloorY}, {FloorZ}): {BlockName}, CanWalkOn={CanWalkOn}",
            x, y, z, destX, y + 1, destZ, destX, y, destZ, toPlace?.Name ?? "null", MovementHelper.CanWalkOn(toPlace));
        double additionalPlacementCost = 0;

        // Baritone lines 70-94: Check if we can walk on destination floor, or need to place
        if (!MovementHelper.CanWalkOn(toPlace))
        {
            // Baritone line 71
            additionalPlacementCost = context.CostOfPlacingAt(destX, y, destZ);
            if (additionalPlacementCost >= ActionCosts.CostInf)
            {
                context.Logger?.LogDebug("[MovementAscend] CostInf: Cannot place block at ({X}, {Y}, {Z}) - CostOfPlacingAt returned CostInf. HasThrowaway={HasThrowaway}, AllowPlace={AllowPlace}",
                    destX, y, destZ, context.HasThrowaway, context.AllowPlace);
                return ActionCosts.CostInf; // Baritone lines 72-73
            }
            // Baritone lines 75-77
            if (!MovementHelper.IsReplaceable(toPlace))
            {
                context.Logger?.LogDebug("[MovementAscend] CostInf: Block at ({X}, {Y}, {Z}) is not replaceable. Block: {BlockName}",
                    destX, y, destZ, toPlace?.Name ?? "null");
                return ActionCosts.CostInf;
            }
            // Baritone lines 78-93: Check for placement option against adjacent blocks
            bool foundPlaceOption = false;
            // HORIZONTALS_BUT_ALSO_DOWN = North, South, East, West, Down
            int[][] directions = [[0, 0, -1], [0, 0, 1], [1, 0, 0], [-1, 0, 0], [0, -1, 0]];
            string[] directionNames = ["North", "South", "East", "West", "Down"];
            for (int i = 0; i < 5; i++)
            {
                int againstX = destX + directions[i][0];
                int againstY = y + directions[i][1];
                int againstZ = destZ + directions[i][2];
                // Baritone line 83-85: Skip if it's our source position
                // Java comment: "we might be able to backplace now, but it doesn't matter because it will have been broken by the time we'd need to use it"
                if (againstX == x && againstZ == z)
                {
                    var sourceBlock = context.GetBlockState(x, y, z);
                    context.Logger?.LogDebug("[MovementAscend] Skipping {Direction} adjacent block at ({X}, {Y}, {Z}) - it's the source position. Source block: {BlockName}, CanPlaceAgainst={CanPlace}",
                        directionNames[i], againstX, againstY, againstZ, sourceBlock?.Name ?? "null", MovementHelper.CanPlaceAgainst(sourceBlock));
                    continue;
                }
                var againstBlock = context.GetBlockState(againstX, againstY, againstZ);
                bool canPlace = MovementHelper.CanPlaceAgainst(againstBlock);
                context.Logger?.LogDebug("[MovementAscend] Checking {Direction} adjacent block at ({X}, {Y}, {Z}): {BlockName}, CanPlaceAgainst={CanPlace}",
                    directionNames[i], againstX, againstY, againstZ, againstBlock?.Name ?? "null", canPlace);
                // Baritone line 86-88
                if (canPlace)
                {
                    foundPlaceOption = true;
                    context.Logger?.LogDebug("[MovementAscend] Found placement option: {Direction} block at ({X}, {Y}, {Z})",
                        directionNames[i], againstX, againstY, againstZ);
                    break;
                }
            }
            // Baritone lines 91-93
            if (!foundPlaceOption)
            {
                context.Logger?.LogDebug("[MovementAscend] CostInf: No placement option found for block at ({X}, {Y}, {Z}). Checked adjacent blocks.",
                    destX, y, destZ);
                return ActionCosts.CostInf;
            }
        }

        // Baritone lines 95-113: FallingBlock check
        var srcUp2 = context.GetBlockState(x, y + 2, z);
        var srcUp3 = context.GetBlockState(x, y + 3, z);
        var srcUp1 = context.GetBlockState(x, y + 1, z);
        if (MovementHelper.IsFallingBlock(srcUp3) && 
            (MovementHelper.CanWalkThrough(srcUp1) || !MovementHelper.IsFallingBlock(srcUp2)))
        {
            context.Logger?.LogDebug("[MovementAscend] CostInf: Falling block at ({X}, {Y}, {Z}) would fall on player. srcUp1={Up1}, srcUp2={Up2}, srcUp3={Up3}",
                x, y + 3, z, srcUp1?.Name ?? "null", srcUp2?.Name ?? "null", srcUp3?.Name ?? "null");
            // Baritone line 108
            return ActionCosts.CostInf;
        }

        // Baritone lines 114-117: Can't ascend from ladder/vine
        var srcDown = context.GetBlockState(x, y - 1, z);
        if (MovementHelper.IsClimbable(srcDown))
        {
            context.Logger?.LogDebug("[MovementAscend] CostInf: Cannot ascend from climbable block at ({X}, {Y}, {Z}). Block: {BlockName}",
                x, y - 1, z, srcDown?.Name ?? "null");
            return ActionCosts.CostInf;
        }

        // Baritone lines 119-123: Bottom slab restrictions
        bool jumpingFromBottomSlab = MovementHelper.IsBottomSlab(srcDown);
        bool jumpingToBottomSlab = MovementHelper.IsBottomSlab(toPlace);
        if (jumpingFromBottomSlab && !jumpingToBottomSlab)
        {
            context.Logger?.LogDebug("[MovementAscend] CostInf: Cannot jump from bottom slab to non-bottom-slab. srcDown={SrcDown}, toPlace={ToPlace}",
                srcDown?.Name ?? "null", toPlace?.Name ?? "null");
            return ActionCosts.CostInf;
        }

        // Baritone lines 124-140: Calculate walk cost
        double walk;
        if (jumpingToBottomSlab)
        {
            if (jumpingFromBottomSlab)
            {
                // Baritone lines 126-128
                walk = Math.Max(ActionCosts.JumpOneBlockCost, ActionCosts.WalkOneBlockCost);
                walk += context.JumpPenalty;
            }
            else
            {
                // Baritone line 130
                walk = ActionCosts.WalkOneBlockCost;
            }
        }
        else
        {
            // Baritone lines 133-139
            if (MovementHelper.IsSoulSand(toPlace))
            {
                walk = ActionCosts.WalkOneOverSoulSandCost;
            }
            else
            {
                walk = Math.Max(ActionCosts.JumpOneBlockCost, ActionCosts.WalkOneBlockCost);
            }
            walk += context.JumpPenalty;
        }

        // Baritone lines 142-154: Total cost calculation
        double totalCost = walk + additionalPlacementCost;
        
        // Baritone line 145: Add mining cost for srcUp2
        var srcUp2Cost = MovementHelper.GetMiningDurationTicks(context, x, y + 2, z, false);
        totalCost += srcUp2Cost;
        if (totalCost >= ActionCosts.CostInf)
        {
            context.Logger?.LogDebug("[MovementAscend] CostInf: Mining cost for srcUp2 at ({X}, {Y}, {Z}) is CostInf. Cost: {Cost}",
                x, y + 2, z, srcUp2Cost);
            return ActionCosts.CostInf;
        }
        
        // Baritone line 149: Add mining cost for dest body
        var destBodyCost = MovementHelper.GetMiningDurationTicks(context, destX, y + 1, destZ, false);
        totalCost += destBodyCost;
        if (totalCost >= ActionCosts.CostInf)
        {
            context.Logger?.LogDebug("[MovementAscend] CostInf: Mining cost for dest body at ({X}, {Y}, {Z}) is CostInf. Cost: {Cost}",
                destX, y + 1, destZ, destBodyCost);
            return ActionCosts.CostInf;
        }
        
        // Baritone line 153: Add mining cost for dest head (includeFalling=true)
        var destHeadCost = MovementHelper.GetMiningDurationTicks(context, destX, y + 2, destZ, true);
        totalCost += destHeadCost;

        Cost = totalCost;
        return Cost;
    }

    /// <summary>
    /// Get blocks that need to be broken for this movement.
    /// Reference: Based on Baritone constructor line 42 - blocksToBreak array
    /// </summary>
    public override IEnumerable<(int X, int Y, int Z)> GetBlocksToBreak(CalculationContext context)
    {
        var destX = Destination.X;
        var destY = Destination.Y;
        var destZ = Destination.Z;
        var srcX = Source.X;
        var srcY = Source.Y;
        var srcZ = Source.Z;

        // Baritone line 42: new BetterBlockPos[]{dest, src.above(2), dest.above()}
        var dest = context.GetBlockState(destX, destY, destZ);
        if (!MovementHelper.CanWalkThrough(dest)) yield return (destX, destY, destZ);

        var srcUp2 = context.GetBlockState(srcX, srcY + 2, srcZ);
        if (!MovementHelper.CanWalkThrough(srcUp2)) yield return (srcX, srcY + 2, srcZ);

        var destUp = context.GetBlockState(destX, destY + 1, destZ);
        if (!MovementHelper.CanWalkThrough(destUp)) yield return (destX, destY + 1, destZ);
    }

    /// <summary>
    /// Get blocks that need to be placed for this movement.
    /// Reference: Based on Baritone constructor line 42 - positionToPlace = dest.below()
    /// </summary>
    public override IEnumerable<(int X, int Y, int Z)> GetBlocksToPlace(CalculationContext context)
    {
        var destX = Destination.X;
        var destY = Destination.Y;
        var destZ = Destination.Z;

        var toPlace = context.GetBlockState(destX, destY - 1, destZ);
        if (!MovementHelper.CanWalkOn(toPlace) && context.HasThrowaway && MovementHelper.IsReplaceable(toPlace))
        {
            yield return (destX, destY - 1, destZ);
        }
    }

    /// <summary>
    /// Update movement state each tick.
    /// Reference: Baritone lines 158-222 - updateState() method
    /// </summary>
    public override MovementState UpdateState(Entity entity, Level level)
    {
        var feet = GetFeetPosition(entity);

        // Baritone lines 159-162: Check if fallen below start
        if (feet.Y < Source.Y)
        {
            State.ClearInputs();
            State.Status = MovementStatus.Unreachable;
            return State;
        }

        // Note: Baritone calls super.updateState(state) here which handles block breaking
        // We handle that separately below

        // Baritone line 166-168: Check if not running
        if (State.Status != MovementStatus.Running)
        {
            State.Status = MovementStatus.Running; // Start running
        }

        // Baritone lines 170-172: Check for success
        // dest OR dest.offset(getDirection().below()) - the latter handles overshoot
        var dx = Destination.X - Source.X;
        var dz = Destination.Z - Source.Z;
        var offsetPos = (Destination.X + dx, Destination.Y - 1, Destination.Z + dz);
        
        if ((feet.X == Destination.X && feet.Y == Destination.Y && feet.Z == Destination.Z) ||
            (feet.X == offsetPos.Item1 && feet.Y == offsetPos.Item2 && feet.Z == offsetPos.Item3))
        {
            State.Status = MovementStatus.Success;
            return State;
        }

        State.ClearInputs();
        State.BreakBlockTarget = null;
        State.PlaceBlockTarget = null;

        // Handle block breaking (this is what super.updateState does in Baritone)
        // Check srcUp2
        var srcUp2 = level.GetBlockAt(Source.X, Source.Y + 2, Source.Z);
        if (srcUp2 != null && !MovementHelper.CanWalkThrough(srcUp2))
        {
            State.BreakBlockTarget = (Source.X, Source.Y + 2, Source.Z);
            return State;
        }
        // Check dest body
        var destBody = level.GetBlockAt(Destination.X, Destination.Y, Destination.Z);
        if (destBody != null && !MovementHelper.CanWalkThrough(destBody))
        {
            State.BreakBlockTarget = (Destination.X, Destination.Y, Destination.Z);
            return State;
        }
        // Check dest head
        var destHead = level.GetBlockAt(Destination.X, Destination.Y + 1, Destination.Z);
        if (destHead != null && !MovementHelper.CanWalkThrough(destHead))
        {
            State.BreakBlockTarget = (Destination.X, Destination.Y + 1, Destination.Z);
            return State;
        }

        // Baritone lines 174-189: Check if we need to place a block
        var jumpingOnto = level.GetBlockAt(Destination.X, Destination.Y - 1, Destination.Z);
        if (!MovementHelper.CanWalkOn(jumpingOnto))
        {
            // Baritone line 176
            _ticksWithoutPlacement++;
            
            // Baritone lines 177-182: Attempt to place
            State.Sneak = true;
            State.RightClick = true;
            State.PlaceBlockTarget = (Destination.X, Destination.Y - 1, Destination.Z);
            
            // Baritone lines 183-186
            if (_ticksWithoutPlacement > 10)
            {
                State.MoveBackward = true;
            }
            
            // Baritone line 188: CRITICAL - return early
            return State;
        }

        // Baritone line 190: Move towards destination
        MoveTowards(entity);

        // Baritone lines 191-193: Don't jump when walking from non-slab into bottom slab
        var srcDown = level.GetBlockAt(Source.X, Source.Y - 1, Source.Z);
        if (MovementHelper.IsBottomSlab(jumpingOnto) && !MovementHelper.IsBottomSlab(srcDown))
        {
            return State;
        }

        // Baritone lines 195-198: assumeStep check (we don't have settings, skip)
        // Also check if already above source
        if (feet.Y >= Source.Y + 1)
        {
            return State;
        }

        // Baritone lines 200-203: Calculate distances
        int xAxis = Math.Abs(Source.X - Destination.X);
        int zAxis = Math.Abs(Source.Z - Destination.Z);
        double flatDistToNext = xAxis * Math.Abs((Destination.X + 0.5) - entity.Position.X) 
                              + zAxis * Math.Abs((Destination.Z + 0.5) - entity.Position.Z);
        double sideDist = zAxis * Math.Abs((Destination.X + 0.5) - entity.Position.X) 
                        + xAxis * Math.Abs((Destination.Z + 0.5) - entity.Position.Z);

        // Baritone lines 205-208: Check lateral motion
        double lateralMotion = xAxis * entity.Velocity.Z + zAxis * entity.Velocity.X;
        if (Math.Abs(lateralMotion) > 0.1)
        {
            return State;
        }

        // Baritone lines 210-212: HeadBonkClear check
        if (HeadBonkClear(level))
        {
            State.Jump = true;
            return State;
        }

        // Baritone lines 214-216: Distance check
        if (flatDistToNext > 1.2 || sideDist > 0.2)
        {
            return State;
        }

        // Baritone lines 218-221: Start jumping
        State.Jump = true;
        return State;
    }

    /// <summary>
    /// Check if head is clear for jumping.
    /// Reference: Baritone lines 224-234 - headBonkClear() method
    /// </summary>
    private bool HeadBonkClear(Level level)
    {
        // Baritone line 225: BetterBlockPos startUp = src.above(2);
        var headY = Source.Y + 2;
        
        // Baritone lines 226-232: Check 4 cardinal directions
        // Direction.from2DDataValue: 0=South, 1=West, 2=North, 3=East
        int[][] cardinals = [[0, 1], [-1, 0], [0, -1], [1, 0]]; // S, W, N, E
        for (int i = 0; i < 4; i++)
        {
            var check = level.GetBlockAt(Source.X + cardinals[i][0], headY, Source.Z + cardinals[i][1]);
            if (!MovementHelper.CanWalkThrough(check))
            {
                return false; // Baritone lines 229-230
            }
        }
        return true; // Baritone line 233
    }

    /// <summary>
    /// Reset movement state.
    /// Reference: Baritone lines 46-49 - reset() method
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        _ticksWithoutPlacement = 0; // Baritone line 48
    }

    /// <summary>
    /// Check if safe to cancel this movement.
    /// Reference: Baritone lines 237-240 - safeToCancel() method
    /// </summary>
    public override bool SafeToCancel()
    {
        // Baritone line 239
        return State.Status != MovementStatus.Running || _ticksWithoutPlacement == 0;
    }
}
