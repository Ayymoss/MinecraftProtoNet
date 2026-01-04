/*
 * This file is part of Baritone.
 *
 * Baritone is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Baritone is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with Baritone.  If not, see <https://www.gnu.org/licenses/>.
 *
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementTraverse.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Physics;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for traversing horizontally to an adjacent block.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementTraverse.java
/// </summary>
public class MovementTraverse(IBaritone baritone, BetterBlockPos from, BetterBlockPos to)
    : Movement(baritone, from, to, [to.Above(), to], to.Below())
{
    /// <summary>
    /// Did we have to place a bridge block or was it always there.
    /// </summary>
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future bridge tracking logic
    private bool _wasTheBridgeBlockAlwaysThere = true;
#pragma warning restore CS0414

    public override void Reset()
    {
        base.Reset();
        _wasTheBridgeBlockAlwaysThere = true;
    }

    public override double CalculateCost(CalculationContext context)
    {
        return Cost(context, Src.X, Src.Y, Src.Z, Dest.X, Dest.Z);
    }

    protected override HashSet<BetterBlockPos> CalculateValidPositions()
    {
        return new HashSet<BetterBlockPos> { Src, Dest };
    }

    public static double Cost(CalculationContext context, int x, int y, int z, int destX, int destZ)
    {
        var pb0 = context.Get(destX, y + 1, destZ);
        var pb1 = context.Get(destX, y, destZ);
        var destOn = context.Get(destX, y - 1, destZ);
        var srcDown = context.Get(x, y - 1, z);
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:63
        bool standingOnABlock = MovementHelper.MustBeSolidToWalkOn(context, x, y - 1, z, srcDown);
        bool frostWalker = standingOnABlock && !context.AssumeWalkOnWater && MovementHelper.CanUseFrostWalker(context, destOn);
        
        if (frostWalker || MovementHelper.CanWalkOn(context, destX, y - 1, destZ, destOn))
        {
            double wc = ActionCosts.WalkOneBlockCost;
            bool water = false;
            if (MovementHelper.IsWater(pb0) || MovementHelper.IsWater(pb1))
            {
                wc = context.WaterWalkSpeed;
                water = true;
            }
            else
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:78-80
                // Check for soul sand (slows movement)
                string destOnName = destOn.Name;
                if (destOnName.Contains("soul_sand", StringComparison.OrdinalIgnoreCase))
                {
                    wc *= 2.0; // Soul sand slows movement
                }
            }
            
            double hardness1 = MovementHelper.GetMiningDurationTicks(context, destX, y, destZ, pb1, false);
            if (hardness1 >= ActionCosts.CostInf)
            {
                return ActionCosts.CostInf;
            }
            double hardness2 = MovementHelper.GetMiningDurationTicks(context, destX, y + 1, destZ, pb0, true);
            if (hardness1 == 0 && hardness2 == 0)
            {
                if (!water && context.CanSprint)
                {
                    wc *= ActionCosts.SprintMultiplier;
                }
                return wc;
            }
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:96-97
            // Check for ladder/vine penalty
            string pb1Name = pb1.Name;
            bool isClimbable = pb1Name.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                              pb1Name.Contains("vine", StringComparison.OrdinalIgnoreCase);
            if (isClimbable)
            {
                wc *= 2.0; // Climbable blocks slow movement
            }
            return wc + hardness1 + hardness2;
        }
        else
        {
            // This is a bridge, so we need to place a block
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:102
            // Check for ladder/vine (can't place blocks on climbable blocks)
            string destOnName = destOn.Name;
            bool isClimbable = destOnName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                              destOnName.Contains("vine", StringComparison.OrdinalIgnoreCase);
            if (isClimbable)
            {
                return ActionCosts.CostInf; // Can't place blocks on climbable blocks
            }
            if (MovementHelper.IsReplaceable(destX, y - 1, destZ, destOn, context.Bsi))
            {
                bool throughWater = MovementHelper.IsWater(pb0) || MovementHelper.IsWater(pb1);
                if (MovementHelper.IsWater(destOn) && throughWater)
                {
                    return ActionCosts.CostInf;
                }
                double placeCost = context.CostOfPlacingAt(destX, y - 1, destZ, destOn);
                if (placeCost >= ActionCosts.CostInf)
                {
                    return ActionCosts.CostInf;
                }
                double hardness1 = MovementHelper.GetMiningDurationTicks(context, destX, y, destZ, pb1, false);
                if (hardness1 >= ActionCosts.CostInf)
                {
                    return ActionCosts.CostInf;
                }
                double hardness2 = MovementHelper.GetMiningDurationTicks(context, destX, y + 1, destZ, pb0, true);
                double wc = throughWater ? context.WaterWalkSpeed : ActionCosts.WalkOneBlockCost;
                
                // Check for side place options
                for (int i = 0; i < 5; i++)
                {
                    var dir = Movement.HorizontalsButAlsoDown[i];
                    var normal = Direction.GetNormal(dir);
                    int againstX = destX + normal.X;
                    int againstY = y - 1 + normal.Y;
                    int againstZ = destZ + normal.Z;
                    if (againstX == x && againstZ == z)
                    {
                        continue; // backplace
                    }
                    if (MovementHelper.CanPlaceAgainst(context.Bsi, againstX, againstY, againstZ))
                    {
                        return wc + placeCost + hardness1 + hardness2;
                    }
                }
                
                // Backplace logic
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:142
                // Check for soul sand, slabs, etc.
                string srcDownName = srcDown.Name;
                bool onSoulSand = srcDownName.Contains("soul_sand", StringComparison.OrdinalIgnoreCase);
                bool onSlab = srcDown.IsSlab;
                if (!standingOnABlock && !onSoulSand && !onSlab)
                {
                    return ActionCosts.CostInf;
                }
                wc = wc * (ActionCosts.SneakOneBlockCost / ActionCosts.WalkOneBlockCost);
                return wc + placeCost + hardness1 + hardness2;
            }
            return ActionCosts.CostInf;
        }
    }

    protected override MovementState UpdateState(MovementState state)
    {
        base.UpdateState(state);
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:157-254
        // Complex update logic from Java implementation
        // This includes door/gate handling, bridge placement, sprint logic, etc.
        
        var pb0 = BlockStateInterface.Get(Ctx, PositionsToBreak[0]);
        var pb1 = BlockStateInterface.Get(Ctx, PositionsToBreak[1]);
        
        if (state.GetStatus() != MovementStatus.Running)
        {
            // Walk while breaking logic
            if (!Core.Baritone.Settings().WalkWhileBreaking.Value)
            {
                return state;
            }
            if (state.GetStatus() != MovementStatus.Prepping)
            {
                return state;
            }
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:174
            // Additional walk-while-breaking checks
            // Check if blocks are breakable
            if (pb0.BlocksMotion && MovementHelper.GetMiningDurationTicks(new CalculationContext(Baritone), PositionsToBreak[0].X, PositionsToBreak[0].Y, PositionsToBreak[0].Z, pb0, false) >= ActionCosts.CostInf)
            {
                return state;
            }
        }

        state.SetInput(Input.Sneak, false);

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:179-181
        // Door and fence gate handling
        var destState = BlockStateInterface.Get(Ctx, Dest);
        string destName = destState.Name;
        bool isDoor = destName.Contains("door", StringComparison.OrdinalIgnoreCase);
        bool isFenceGate = destName.Contains("fence_gate", StringComparison.OrdinalIgnoreCase);
        if (isDoor || isFenceGate)
        {
            // TODO: Check if door/gate is open and handle opening if needed
        }
        
        // Bridge block checking
        // Movement towards destination

        var feet = Ctx.PlayerFeet();
        if (feet != null && feet.Equals(Dest))
        {
            return state.SetStatus(MovementStatus.Success);
        }

        bool isTheBridgeBlockThere = PositionToPlace != null && MovementHelper.CanWalkOn(Ctx, PositionToPlace);
        
        if (isTheBridgeBlockThere)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:193-195
            // Sprint logic, movement towards dest
            var context = new CalculationContext(Baritone);
            if (context.CanSprint && !MovementHelper.IsWater(pb0) && !MovementHelper.IsWater(pb1))
            {
                state.SetInput(Input.Sprint, true);
            }
            MovementHelper.MoveTowards(Ctx, state, PositionsToBreak[0]);
            return state;
        }
        else
        {
            _wasTheBridgeBlockAlwaysThere = false;
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:200-202
            // Block placement logic
            var placeResult = MovementHelper.AttemptToPlaceABlock(state, Baritone, Dest.Below(), false, true);
            // Handle place result
            if (placeResult == MovementHelper.PlaceResult.ReadyToPlace || placeResult == MovementHelper.PlaceResult.Attempting)
            {
                MovementHelper.MoveTowards(Ctx, state, PositionsToBreak[0]);
            }
            return state;
        }
    }

    protected override bool SafeToCancel(MovementState state)
    {
        return state.GetStatus() != MovementStatus.Running || MovementHelper.CanWalkOn(Ctx, Dest.Below());
    }

    protected override bool Prepared(MovementState state)
    {
        var feet = Ctx.PlayerFeet();
        if (feet != null && (feet.Equals(Src) || feet.Equals(Src.Below())))
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementTraverse.java:218
            // Check for ladder/vine and set sneak
            var srcState = BlockStateInterface.Get(Ctx, Src);
            string srcName = srcState.Name;
            bool isClimbable = srcName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                              srcName.Contains("vine", StringComparison.OrdinalIgnoreCase);
            if (isClimbable)
            {
                state.SetInput(Input.Sneak, true);
            }
        }
        return base.Prepared(state);
    }
}

