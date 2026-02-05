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
using MinecraftProtoNet.Core.State;
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

    public override MovementState UpdateState(MovementState state)
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
        // Check for water pillar
        if (MovementHelper.IsWater(fromDown) && MovementHelper.IsWater(BlockStateInterface.Get(Ctx, Src.Above())))
        {
            var destCenter = VecUtils.GetBlockPosCenter(Dest);
            var head = Ctx.PlayerHead();
            var rots = Ctx.PlayerRotations();
            
            if (head != null && rots != null)
            {
                state.SetTarget(new MovementState.MovementTarget(
                    RotationUtils.CalcRotationFromVec3d(head, destCenter, rots), 
                    false));
            }

            var playerPos = (Ctx.Player() as Entity)?.Position;
            if (playerPos != null && (Math.Abs(playerPos.X - destCenter.X) > 0.2 || Math.Abs(playerPos.Z - destCenter.Z) > 0.2))
            {
                state.SetInput(BaritoneInput.MoveForward, true);
            }
            
            if (feet != null && feet.Equals(Dest))
            {
                return state.SetStatus(MovementStatus.Success);
            }
            return state;
        }
        
        string fromDownName = fromDown.Name;
        bool ladder = fromDownName.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                     fromDownName.Contains("vine", StringComparison.OrdinalIgnoreCase);
        bool vine = fromDownName.Contains("vine", StringComparison.OrdinalIgnoreCase);
        
        // Rotation calculation
        var playerHead = Ctx.PlayerHead();
        var playerRotations = Ctx.PlayerRotations();
        
        // Guard against nulls
        if (playerHead == null || playerRotations == null) return state;

        var placeTarget = PositionToPlace ?? Src;
        Rotation rotation = RotationUtils.CalcRotationFromVec3d(
            playerHead,
            VecUtils.GetBlockPosCenter(placeTarget),
            playerRotations);

        if (!ladder)
        {
            state.SetTarget(new MovementState.MovementTarget(playerRotations.WithPitch(rotation.GetPitch()), true));
        }
        
        bool blockIsThere = MovementHelper.CanWalkOn(Ctx, Src) || ladder;
        
        if (ladder)
        {
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
            // Block placement logic
            var inventoryBehavior = ((Core.Baritone)Baritone).GetInventoryBehavior();
            if (!inventoryBehavior.SelectThrowawayForLocation(true, Src.X, Src.Y, Src.Z))
            {
                return state.SetStatus(MovementStatus.Unreachable);
            }
            
            var player = Ctx.Player() as Entity;
            if (player == null) return state;

            // Sneak logic to delay placement
            bool shouldSneak = player.Position.Y > Dest.Y || player.Position.Y < Src.Y + 0.2;
            state.SetInput(BaritoneInput.Sneak, shouldSneak);

            // Centering logic
            double diffX = player.Position.X - (Dest.X + 0.5);
            double diffZ = player.Position.Z - (Dest.Z + 0.5);
            double dist = Math.Sqrt(diffX * diffX + diffZ * diffZ);
            double flatMotion = Math.Sqrt(player.Velocity.X * player.Velocity.X + player.Velocity.Z * player.Velocity.Z);

            if (dist > 0.17)
            {
                state.SetInput(BaritoneInput.MoveForward, true);
                state.SetTarget(new MovementState.MovementTarget(rotation, true));
            }
            else if (flatMotion < 0.05)
            {
                if (player.Position.Y < Dest.Y)
                {
                    state.SetInput(BaritoneInput.Jump, true);
                }
            }

            if (!blockIsThere)
            {
                bool isLookingAtSrc = Ctx.IsLookingAt(Src) || Ctx.IsLookingAt(Src.Below());
                bool isSneaking = state.GetInput(BaritoneInput.Sneak) || player.IsSneaking;
                
                if (isSneaking && isLookingAtSrc && player.Position.Y > Dest.Y + 0.1)
                {
                    state.SetInput(BaritoneInput.ClickRight, true);
                }
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

