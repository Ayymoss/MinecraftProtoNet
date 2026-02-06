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
using MinecraftProtoNet.Core.State;

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
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementTraverse.java:55
    /// </summary>
    private bool _wasTheBridgeBlockAlwaysThere = true;

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
            if (hardness2 >= ActionCosts.CostInf)
            {
                return ActionCosts.CostInf;
            }
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
                // Baritone.GetGameEventHandler().LogDirect("[DEBUG] Traverse: Failed to bridge because target is climbable");
                return ActionCosts.CostInf; // Can't place blocks on climbable blocks
            }
            if (MovementHelper.IsReplaceable(destX, y - 1, destZ, destOn, context.Bsi))
            {
                bool throughWater = MovementHelper.IsWater(pb0) || MovementHelper.IsWater(pb1);
                if (MovementHelper.IsWater(destOn) && throughWater)
                {
                    // Baritone.GetGameEventHandler().LogDirect("[DEBUG] Traverse: Failed to bridge because placing in water while in water");
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
                string srcDownName = srcDown.Name;
                bool onSoulSand = srcDownName.Contains("soul_sand", StringComparison.OrdinalIgnoreCase);
                bool onSlab = srcDown.IsSlab;
                
                if (onSoulSand || (onSlab && (!srcDown.Properties.TryGetValue("type", out var type) || type != "double")))
                {
                    return ActionCosts.CostInf;
                }
                
                if (!standingOnABlock && !srcDown.IsAir)
                {
                    // Baritone.GetGameEventHandler().LogDirect($"[DEBUG] Traverse: Failed backplace - Not standing on a solid block ({x},{y-1},{z}) Name={srcDown.Name}");
                    return ActionCosts.CostInf;
                }
                
                wc = wc * (ActionCosts.SneakOneBlockCost / ActionCosts.WalkOneBlockCost);
                return wc + placeCost + hardness1 + hardness2;
            }
            return ActionCosts.CostInf;
        }
    }

    public override MovementState UpdateState(MovementState state)
    {
        base.UpdateState(state);
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementTraverse.java:172-358
        
        var pb0 = BlockStateInterface.Get(Ctx, PositionsToBreak[0]);
        var pb1 = BlockStateInterface.Get(Ctx, PositionsToBreak[1]);
        
        if (state.GetStatus() != MovementStatus.Running)
        {
            // Walk while breaking logic
            // Reference: MovementTraverse.java:178
            if (!Core.Baritone.Settings().WalkWhileBreaking.Value)
            {
                return state;
            }
            if (state.GetStatus() != MovementStatus.Prepping)
            {
                return state;
            }
            // Reference: MovementTraverse.java:186-191
            if (MovementHelper.AvoidWalkingInto(pb0))
            {
                return state;
            }
            if (MovementHelper.AvoidWalkingInto(pb1))
            {
                return state;
            }
            // Check if we aren't already pressed up against the block
            var playerWb = Ctx.Player() as Entity;
            if (playerWb == null) return state;
            double distWhileBreaking = Math.Max(Math.Abs(playerWb.Position.X - (Dest.X + 0.5)), Math.Abs(playerWb.Position.Z - (Dest.Z + 0.5)));
            if (distWhileBreaking < 0.83)
            {
                return state;
            }
            var targetRotWb = state.GetTarget()?.GetRotation();
            if (targetRotWb == null)
            {
                return state;
            }
            // Combine yaw to center of dest and pitch to block we're breaking
            var playerHeadWb = Ctx.PlayerHead();
            var playerRotWb = Ctx.PlayerRotations();
            if (playerHeadWb == null || playerRotWb == null) return state;
            float yawToDest = RotationUtils.CalcRotationFromVec3d(playerHeadWb, VecUtils.GetBlockPosCenter(Dest), playerRotWb).GetYaw();
            float pitchToBreak = targetRotWb.GetPitch();
            if ((MovementHelper.IsBlockNormalCube(pb0) || pb0.IsAir) && (MovementHelper.IsBlockNormalCube(pb1) || pb1.IsAir))
            {
                pitchToBreak = 26;
            }
            return state.SetTarget(new MovementState.MovementTarget(new Rotation(yawToDest, pitchToBreak), true))
                .SetInput(Input.MoveForward, true)
                .SetInput(Input.Sprint, true);
        }

        // Sneak may have been set to true in the PREPPING state while mining an adjacent block
        // Reference: MovementTraverse.java:217
        state.SetInput(Input.Sneak, false);

        // Reference: MovementTraverse.java:219-220
        var srcBelowState = BlockStateInterface.Get(Ctx, Src.Below());
        bool ladder = srcBelowState.Name.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                      srcBelowState.Name.Contains("vine", StringComparison.OrdinalIgnoreCase);

        // Reference: MovementTraverse.java:222-242 - Door and fence gate handling
        if (pb0.Name.Contains("door", StringComparison.OrdinalIgnoreCase) || pb1.Name.Contains("door", StringComparison.OrdinalIgnoreCase))
        {
            // TODO: Check if door is passable and handle opening if needed
        }
        if (pb0.Name.Contains("fence_gate", StringComparison.OrdinalIgnoreCase) || pb1.Name.Contains("fence_gate", StringComparison.OrdinalIgnoreCase))
        {
            // TODO: Check if fence gate is passable and handle opening if needed
        }

        // Reference: MovementTraverse.java:244
        bool isTheBridgeBlockThere = (PositionToPlace != null && MovementHelper.CanWalkOn(Ctx, PositionToPlace)) || ladder;
        
        var feet = Ctx.PlayerFeet();
        
        // Reference: MovementTraverse.java:246-253 - CRITICAL Y-coordinate check
        if (feet != null && feet.Y != Dest.Y && !ladder)
        {
            if (feet.Y < Dest.Y)
            {
                return state.SetInput(Input.Jump, true);
            }
            return state;
        }

        if (isTheBridgeBlockThere)
        {
            // Reference: MovementTraverse.java:256-257
            if (feet != null && feet.Equals(Dest))
            {
                return state.SetStatus(MovementStatus.Success);
            }
            
            // Reference: MovementTraverse.java:259-261 - Overshoot traverse check
            var direction = GetDirection();
            if (Core.Baritone.Settings().OvershootTraverse.Value && feet != null)
            {
                var overshoot1 = new BetterBlockPos(Dest.X + direction.X, Dest.Y + direction.Y, Dest.Z + direction.Z);
                var overshoot2 = new BetterBlockPos(Dest.X + direction.X * 2, Dest.Y + direction.Y * 2, Dest.Z + direction.Z * 2);
                if (feet.Equals(overshoot1) || feet.Equals(overshoot2))
                {
                    return state.SetStatus(MovementStatus.Success);
                }
            }

            // Reference: MovementTraverse.java:262-268 - Ladder/vine climbing check
            var player = Ctx.Player() as Entity;
            if (player != null)
            {
                var lowBlock = BlockStateInterface.Get(Ctx, Src);
                var highBlock = BlockStateInterface.Get(Ctx, Src.Above());
                bool lowClimbable = lowBlock.Name.Contains("vine", StringComparison.OrdinalIgnoreCase) || 
                                    lowBlock.Name.Contains("ladder", StringComparison.OrdinalIgnoreCase);
                bool highClimbable = highBlock.Name.Contains("vine", StringComparison.OrdinalIgnoreCase) || 
                                     highBlock.Name.Contains("ladder", StringComparison.OrdinalIgnoreCase);
                if (player.Position.Y > Src.Y + 0.1 && !player.IsOnGround && (lowClimbable || highClimbable))
                {
                    // Hitting W could cause us to climb the ladder instead of going forward; wait until on ground
                    return state;
                }
            }

            // Reference: MovementTraverse.java:269-274 - Sprint logic with safety checks
            var into = new BetterBlockPos(Dest.X + (Dest.X - Src.X), Dest.Y + (Dest.Y - Src.Y), Dest.Z + (Dest.Z - Src.Z));
            var intoBelow = BlockStateInterface.Get(Ctx, into);
            var intoAbove = BlockStateInterface.Get(Ctx, into.Above());
            if (_wasTheBridgeBlockAlwaysThere 
                && (!MovementHelper.IsLiquid(Ctx, feet ?? Src) || Core.Baritone.Settings().SprintInWater.Value)
                && (!MovementHelper.AvoidWalkingInto(intoBelow) || MovementHelper.IsWater(intoBelow)) 
                && !MovementHelper.AvoidWalkingInto(intoAbove))
            {
                state.SetInput(Input.Sprint, true);
            }

            // Reference: MovementTraverse.java:276-286 - Move towards dest
            var against = PositionsToBreak[0]; // defaults to dest.above()
            if (feet != null && feet.Y != Dest.Y && ladder)
            {
                var destDown = BlockStateInterface.Get(Ctx, Dest.Below());
                if (destDown.Name.Contains("ladder", StringComparison.OrdinalIgnoreCase) || destDown.Name.Contains("vine", StringComparison.OrdinalIgnoreCase))
                {
                    // For ladder/vine descent, use the block the ladder is attached to
                    // Simplified: just use dest.below() as target
                    against = Dest.Below();
                }
            }
            MovementHelper.MoveTowards(Ctx, state, against);
            return state;
        }
        else
        {
            // Reference: MovementTraverse.java:288
            _wasTheBridgeBlockAlwaysThere = false;
            
            // Reference: MovementTraverse.java:289-297 - Soul sand / slab handling (see issue #118)
            var playerSs = Ctx.Player() as Entity;
            if (feet != null && playerSs != null)
            {
                var standingOn = BlockStateInterface.Get(Ctx, feet.Below());
                if (standingOn.Name.Contains("soul_sand", StringComparison.OrdinalIgnoreCase) || standingOn.IsSlab)
                {
                    double distSoulSand = Math.Max(Math.Abs(Dest.X + 0.5 - playerSs.Position.X), Math.Abs(Dest.Z + 0.5 - playerSs.Position.Z));
                    if (distSoulSand < 0.85) // 0.5 + 0.3 + epsilon
                    {
                        MovementHelper.MoveTowards(Ctx, state, Dest);
                        return state.SetInput(Input.MoveForward, false)
                            .SetInput(Input.MoveBack, true);
                    }
                }
            }

            // Reference: MovementTraverse.java:298-299
            double dist1 = 0;
            var playerBridge = Ctx.Player() as Entity;
            if (playerBridge != null)
            {
                dist1 = Math.Max(Math.Abs(playerBridge.Position.X - (Dest.X + 0.5)), Math.Abs(playerBridge.Position.Z - (Dest.Z + 0.5)));
            }

            var placeResult = MovementHelper.AttemptToPlaceABlock(state, Baritone, Dest.Below(), false, true);
            
            // Reference: MovementTraverse.java:300-302 - Sneak when close or ready to place
            if ((placeResult == MovementHelper.PlaceResult.ReadyToPlace || dist1 < 0.6) && !Core.Baritone.Settings().AssumeSafeWalk.Value)
            {
                state.SetInput(Input.Sneak, true);
            }

            switch (placeResult)
            {
                // Reference: MovementTraverse.java:304-309
                case MovementHelper.PlaceResult.ReadyToPlace:
                {
                    if ((playerBridge != null && playerBridge.IsSneaking) || Core.Baritone.Settings().AssumeSafeWalk.Value)
                    {
                        state.SetInput(Input.ClickRight, true);
                    }
                    return state;
                }
                // Reference: MovementTraverse.java:310-323
                // Reference: MovementTraverse.java:310-323
                case MovementHelper.PlaceResult.Attempting:
                {
                    if (dist1 > 0.83)
                    {
                        // Might need to go forward a bit
                        float yaw = RotationUtils.CalcRotationFromVec3d(Ctx.PlayerHead(), VecUtils.GetBlockPosCenter(Dest), Ctx.PlayerRotations()).GetYaw();
                        var targetRot = state.GetTarget()?.GetRotation();
                        if (targetRot != null && Math.Abs(targetRot.GetYaw() - yaw) < 0.1)
                        {
                            // But only if our attempted place is straight ahead
                            return state.SetInput(Input.MoveForward, true);
                        }
                    }
                    else
                    {
                        var targetRot = state.GetTarget()?.GetRotation();
                        if (targetRot != null && Ctx.PlayerRotations().IsReallyCloseTo(targetRot))
                        {
                            // Well I guess there's something in the way
                            return state.SetInput(Input.ClickLeft, true);
                        }
                    }
                    return state;
                }
                default:
                    break;
            }

            // Reference: MovementTraverse.java:327-354 - Backplace logic
            feet = Ctx.PlayerFeet();
            if (feet != null && feet.Equals(Dest))
            {
                // We are in the block that we are trying to get to, sneaking over air.
                // We need to place a block beneath us against the one we just walked off of.
                double faceX = (Dest.X + Src.X + 1.0) * 0.5;
                // Java uses (dest.y + src.y - 1.0) * 0.5 which gives the vertical center of the face (93.5).
                // However, from the player's elevated eye position (Y+1.62), a ray aimed at Y=93.5 can
                // barely clip the TOP face of the against block instead of the intended SIDE face.
                // This causes placement at the wrong position (on top instead of beside).
                // Aim at the lower third of the side face to ensure the ray reliably hits the side face.
                double faceY = Src.Y - 1.0 + 0.25; // Lower on the side face to avoid clipping the top face
                double faceZ = (Dest.Z + Src.Z + 1.0) * 0.5;
                
                var goalLook = Src.Below(); // The block we were just standing on

                var backToFace = RotationUtils.CalcRotationFromVec3d(Ctx.PlayerHead(), new MinecraftProtoNet.Core.Models.Core.Vector3<double>(faceX, faceY, faceZ), Ctx.PlayerRotations());
                float pitch = backToFace.GetPitch();
                
                double dist2 = 0;
                if (playerBridge != null)
                {
                    dist2 = Math.Max(Math.Abs(playerBridge.Position.X - faceX), Math.Abs(playerBridge.Position.Z - faceZ));
                }

                // Reference: MovementTraverse.java:339 - see issue #208
                if (dist2 < 0.29)
                {
                    float yaw = RotationUtils.CalcRotationFromVec3d(VecUtils.GetBlockPosCenter(Dest), Ctx.PlayerHead(), Ctx.PlayerRotations()).GetYaw();
                    state.SetTarget(new MovementState.MovementTarget(new Rotation(yaw, pitch), true));
                    state.SetInput(Input.MoveBack, true);
                }
                else
                {
                    state.SetTarget(new MovementState.MovementTarget(backToFace, true));
                }

                if (Ctx.IsLookingAt(goalLook))
                {
                    return state.SetInput(Input.ClickRight, true); // Wait to right click until we are able to place
                }
                
                var targetRotBackplace = state.GetTarget()?.GetRotation();
                if (targetRotBackplace != null && Ctx.PlayerRotations().IsReallyCloseTo(targetRotBackplace))
                {
                    state.SetInput(Input.ClickLeft, true);
                }
                return state;
            }

            // Reference: MovementTraverse.java:355-356 - Fallback: move towards destination.
            // The intended Java bridging flow is:
            //   1. AttemptToPlaceABlock returns NoOption (can't see side face from above)
            //   2. Bot walks (sneaking) forward towards dest until feet == dest
            //   3. Backplace logic (above) places block beneath the bot against the block it walked off of
            // We MUST sneak here so the bot stops at the edge and doesn't fall off.
            // Minecraft's sneak mechanic prevents the player from walking off block edges.
            if (!Core.Baritone.Settings().AssumeSafeWalk.Value)
            {
                state.SetInput(Input.Sneak, true);
            }
            MovementHelper.MoveTowards(Ctx, state, PositionsToBreak[0]);
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

