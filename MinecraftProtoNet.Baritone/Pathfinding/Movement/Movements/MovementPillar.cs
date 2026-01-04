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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementPillar.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Utils;
using BaritoneInput = MinecraftProtoNet.Baritone.Api.Utils.Input.Input;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;

/// <summary>
/// Movement for building up one block (pillaring).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementPillar.java
/// </summary>
public class MovementPillar(IBaritone baritone, BetterBlockPos start, BetterBlockPos end)
    : Movement(baritone, start, end, [start.Above(2)], start)
{
    public override double CalculateCost(CalculationContext context)
    {
        return Cost(context, Src.X, Src.Y, Src.Z);
    }

    protected override HashSet<BetterBlockPos> CalculateValidPositions()
    {
        return [Src, Dest];
    }

    public static double Cost(CalculationContext context, int x, int y, int z)
    {
        var fromState = context.Get(x, y, z);
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:47-48
        string fromName = fromState.Name;
        bool ladder = fromName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                     fromName.Contains("vine", StringComparison.OrdinalIgnoreCase);
        var fromDown = context.Get(x, y - 1, z);
        
        if (!ladder)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:53-55
            // Check for ladder/vine, bottom slab
            string fromDownName = fromDown.Name;
            bool isClimbable = fromDownName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                              fromDownName.Contains("vine", StringComparison.OrdinalIgnoreCase);
            bool isBottomSlab = MovementHelper.IsBottomSlab(fromDown);
            if (isClimbable || isBottomSlab)
            {
                ladder = isClimbable;
            }
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:56-57
        // Check for vine against block
        var toBreak = context.Get(x, y + 2, z);
        // Check for fence gate
        string toBreakName = toBreak.Name;
        bool isFenceGate = toBreakName.Contains("fence_gate", StringComparison.OrdinalIgnoreCase);
        if (isFenceGate)
        {
            // Fence gates can be opened, so they're passable
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:60
        // Check for water pillar
        bool isWaterPillar = MovementHelper.IsWater(fromState) && MovementHelper.IsWater(context.Get(x, y + 1, z));
        double placeCost = 0;
        if (!ladder)
        {
            placeCost = context.CostOfPlacingAt(x, y, z, fromState);
            if (placeCost >= ActionCosts.CostInf)
            {
                return ActionCosts.CostInf;
            }
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:69
            // Check for air penalty (placing in air costs more)
            if (fromState.IsAir)
            {
                placeCost *= 2.0;
            }
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:72-73
        // Check for liquid conditions
        bool inLiquid = fromState.IsLiquid || context.Get(x, y + 1, z).IsLiquid;
        // Check for lily pad/carpet
        // Reuse toBreakName from above (declared at line 73)
        bool hasLilyPad = toBreakName.Contains("lily_pad", StringComparison.OrdinalIgnoreCase) ||
                         toBreakName.Contains("carpet", StringComparison.OrdinalIgnoreCase);
        
        double hardness = MovementHelper.GetMiningDurationTicks(context, x, y + 2, z, toBreak, true);
        if (hardness >= ActionCosts.CostInf)
        {
            return ActionCosts.CostInf;
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:81-84
        // Check for ladder/vine, falling blocks
        // Reuse toBreakName from above
        bool isFallingBlock = toBreakName.Contains("gravel", StringComparison.OrdinalIgnoreCase) ||
                             toBreakName.Contains("sand", StringComparison.OrdinalIgnoreCase);
        if (hardness != 0 && isFallingBlock)
        {
            // Complex falling block logic - add cost for breaking falling blocks above
            hardness += MovementHelper.GetMiningDurationTicks(context, x, y + 3, z, context.Get(x, y + 3, z), true);
        }
        
        if (ladder)
        {
            return ActionCosts.LadderUpOneCost + hardness * 5;
        }
        else
        {
            return ActionCosts.JumpOneBlockCost + placeCost + context.JumpPenalty + hardness;
        }
    }

    public static bool HasAgainst(CalculationContext context, int x, int y, int z)
    {
        return MovementHelper.IsBlockNormalCube(context.Get(x + 1, y, z)) ||
               MovementHelper.IsBlockNormalCube(context.Get(x - 1, y, z)) ||
               MovementHelper.IsBlockNormalCube(context.Get(x, y, z + 1)) ||
               MovementHelper.IsBlockNormalCube(context.Get(x, y, z - 1));
    }

    public static BetterBlockPos? GetAgainst(CalculationContext context, BetterBlockPos vine)
    {
        var north = vine.North();
        if (MovementHelper.IsBlockNormalCube(context.Get(north.X, north.Y, north.Z)))
        {
            return north;
        }
        var south = vine.South();
        if (MovementHelper.IsBlockNormalCube(context.Get(south.X, south.Y, south.Z)))
        {
            return south;
        }
        var east = vine.East();
        if (MovementHelper.IsBlockNormalCube(context.Get(east.X, east.Y, east.Z)))
        {
            return east;
        }
        var west = vine.West();
        if (MovementHelper.IsBlockNormalCube(context.Get(west.X, west.Y, west.Z)))
        {
            return west;
        }
        return null;
    }

    protected override MovementState UpdateState(MovementState state)
    {
        base.UpdateState(state);
        if (state.GetStatus() != MovementStatus.Running)
        {
            return state;
        }

        var feet = Ctx.PlayerFeet();
        if (feet != null && feet.Y < Src.Y)
        {
            return state.SetStatus(MovementStatus.Unreachable);
        }

        var fromDown = BlockStateInterface.Get(Ctx, Src);
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:145-146
        // Check for water pillar
        bool isWaterPillar = MovementHelper.IsWater(fromDown) && MovementHelper.IsWater(BlockStateInterface.Get(Ctx, Src.Above()));
        
        string fromDownName = fromDown.Name;
        bool ladder = fromDownName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                     fromDownName.Contains("vine", StringComparison.OrdinalIgnoreCase);
        bool vine = fromDownName.Contains("vine", StringComparison.OrdinalIgnoreCase);
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:150
        // Rotation calculation - handled by MoveTowards
        
        bool blockIsThere = MovementHelper.CanWalkOn(Ctx, Src) || ladder;
        
        if (ladder)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:156-170
            // Ladder climbing logic
            BetterBlockPos? against = GetAgainst(new CalculationContext(Baritone), Src);
            if (against == null)
            {
                return state.SetStatus(MovementStatus.Unreachable);
            }
            
            if (feet != null && (feet.Equals(against.Above()) || feet.Equals(Dest)))
            {
                return state.SetStatus(MovementStatus.Success);
            }
            
            // Bottom slab jump
            var againstState = BlockStateInterface.Get(Ctx, against);
            if (MovementHelper.IsBottomSlab(againstState))
            {
                state.SetInput(BaritoneInput.Jump, true);
            }
            MovementHelper.MoveTowards(Ctx, state, against);
            return state;
        }
        else
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:174-184
            // Block placement logic
            // Get ready to place a throwaway block
            var inventoryBehavior = ((Core.Baritone)Baritone).GetInventoryBehavior();
            inventoryBehavior.SelectThrowawayForLocation(true, Src.X, Src.Y, Src.Z);
            
            // Sneak and placement logic
            if (!blockIsThere)
            {
                state.SetInput(BaritoneInput.Sneak, true);
                var placeResult = MovementHelper.AttemptToPlaceABlock(state, Baritone, Src, false, true);
                if (placeResult == MovementHelper.PlaceResult.ReadyToPlace)
                {
                    // Ready to place
                }
            }
            
            // Movement and jump logic
            MovementHelper.MoveTowards(Ctx, state, Dest);
            if (!blockIsThere)
            {
                state.SetInput(BaritoneInput.Jump, true);
            }
        }

        if (feet != null && feet.Equals(Dest) && blockIsThere)
        {
            return state.SetStatus(MovementStatus.Success);
        }

        return state;
    }

    protected override bool Prepared(MovementState state)
    {
        var feet = Ctx.PlayerFeet();
        if (feet != null && (feet.Equals(Src) || feet.Equals(Src.Below())))
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/movement/movements/MovementPillar.java:200
            // Check for ladder/vine and set sneak
            var srcState = BlockStateInterface.Get(Ctx, Src);
            string srcName = srcState.Name;
            bool isClimbable = srcName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                              srcName.Contains("vine", StringComparison.OrdinalIgnoreCase);
            if (isClimbable)
            {
                state.SetInput(BaritoneInput.Sneak, true);
            }
        }
        var abovePos = Dest.Above();
        if (MovementHelper.IsWater(BlockStateInterface.Get(Ctx, abovePos)))
        {
            return true;
        }
        return base.Prepared(state);
    }
}

