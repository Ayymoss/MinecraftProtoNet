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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/RotationUtils.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Utility class for rotation calculations.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/RotationUtils.java
/// </summary>
public static class RotationUtils
{
    /// <summary>
    /// Constant that a degree value is multiplied by to get the equivalent radian value
    /// </summary>
    public const double DegToRad = Math.PI / 180.0;
    public const float DegToRadF = (float)DegToRad;

    /// <summary>
    /// Constant that a radian value is multiplied by to get the equivalent degree value
    /// </summary>
    public const double RadToDeg = 180.0 / Math.PI;
    public const float RadToDegF = (float)RadToDeg;

    /// <summary>
    /// Offsets from the root block position to the center of each side.
    /// </summary>
    private static readonly Vector3<double>[] BlockSideMultipliers = new Vector3<double>[]
    {
        new(0.5, 0, 0.5),    // Down
        new(0.5, 1, 0.5),    // Up
        new(0.5, 0.5, 0),    // North
        new(0.5, 0.5, 1),    // South
        new(0, 0.5, 0.5),    // West
        new(1, 0.5, 0.5)     // East
    };

    /// <summary>
    /// Calculates the rotation from BlockPos dest to BlockPos orig
    /// </summary>
    public static Rotation CalcRotationFromCoords(BetterBlockPos orig, BetterBlockPos dest)
    {
        return CalcRotationFromVec3d(
            new Vector3<double>(orig.X, orig.Y, orig.Z),
            new Vector3<double>(dest.X, dest.Y, dest.Z)
        );
    }

    /// <summary>
    /// Wraps the target angles to a relative value from the current angles.
    /// </summary>
    public static Rotation WrapAnglesToRelative(Rotation current, Rotation target)
    {
        if (current.YawIsReallyClose(target))
        {
            return new Rotation(current.GetYaw(), target.GetPitch());
        }
        return target.Subtract(current).Normalize().Add(current);
    }

    /// <summary>
    /// Calculates the rotation from Vec dest to Vec orig and makes the
    /// return value relative to the specified current rotations.
    /// </summary>
    public static Rotation CalcRotationFromVec3d(Vector3<double> orig, Vector3<double> dest, Rotation current)
    {
        return WrapAnglesToRelative(current, CalcRotationFromVec3d(orig, dest));
    }

    /// <summary>
    /// Calculates the rotation from Vec dest to Vec orig
    /// </summary>
    private static Rotation CalcRotationFromVec3d(Vector3<double> orig, Vector3<double> dest)
    {
        double[] delta = { orig.X - dest.X, orig.Y - dest.Y, orig.Z - dest.Z };
        double yaw = Math.Atan2(delta[0], -delta[2]);
        double dist = Math.Sqrt(delta[0] * delta[0] + delta[2] * delta[2]);
        double pitch = Math.Atan2(delta[1], dist);
        return new Rotation(
            (float)(yaw * RadToDeg),
            (float)(pitch * RadToDeg)
        );
    }

    /// <summary>
    /// Calculates the look vector for the specified yaw/pitch rotations.
    /// </summary>
    public static Vector3<double> CalcLookDirectionFromRotation(Rotation rotation)
    {
        float flatZ = (float)Math.Cos((-rotation.GetYaw() * DegToRadF) - Math.PI);
        float flatX = (float)Math.Sin((-rotation.GetYaw() * DegToRadF) - Math.PI);
        float pitchBase = -(float)Math.Cos(-rotation.GetPitch() * DegToRadF);
        float pitchHeight = (float)Math.Sin(-rotation.GetPitch() * DegToRadF);
        return new Vector3<double>(flatX * pitchBase, pitchHeight, flatZ * pitchBase);
    }

    /// <summary>
    /// @param ctx Context for the viewing entity
    /// @param pos The target block position
    /// @return The optional rotation
    /// </summary>
    public static Rotation? Reachable(IPlayerContext ctx, BetterBlockPos pos)
    {
        return Reachable(ctx, pos, false);
    }

    public static Rotation? Reachable(IPlayerContext ctx, BetterBlockPos pos, bool wouldSneak)
    {
        var playerController = ctx.PlayerController();
        double blockReachDistance = playerController.GetBlockReachDistance();
        return Reachable(ctx, pos, blockReachDistance, wouldSneak);
    }

    /// <summary>
    /// Determines if the specified entity is able to reach the center of any of the sides
    /// of the specified block.
    /// </summary>
    public static Rotation? Reachable(IPlayerContext ctx, BetterBlockPos pos, double blockReachDistance)
    {
        return Reachable(ctx, pos, blockReachDistance, false);
    }

    public static Rotation? Reachable(IPlayerContext ctx, BetterBlockPos pos, double blockReachDistance, bool wouldSneak)
    {
        var settings = BaritoneAPI.GetSettings();
        var currentRot = ctx.PlayerRotations();
        if (currentRot != null && settings.RemainWithExistingLookDirection.Value && ctx.IsLookingAt(pos))
        {
            Rotation hypothetical = currentRot.Add(new Rotation(0, 0.0001F));
            if (wouldSneak)
            {
                var worldForRayTrace = ctx.World() as Level;
                var playerEntity = ctx.Player() as Entity;
                if (playerEntity == null) return null;
        var result = RayTraceUtils.RayTraceTowards(playerEntity, worldForRayTrace, hypothetical, blockReachDistance, true);
                if (result != null && result.Block != null && 
                    result.BlockPosition.X == pos.X && result.BlockPosition.Y == pos.Y && result.BlockPosition.Z == pos.Z)
                {
                    return hypothetical;
                }
            }
            else
            {
                return hypothetical;
            }
        }

        Rotation? possibleRotation = ReachableCenter(ctx, pos, blockReachDistance, wouldSneak);
        if (possibleRotation != null)
        {
            return possibleRotation;
        }

        var world = ctx.World() as Level;
        if (world == null) return null;

        var state = world.GetBlockAt(pos.X, pos.Y, pos.Z);
        if (state == null) return null;

        // For now, use simple block center calculation
        // Full implementation would use VoxelShape calculations
        for (int i = 0; i < BlockSideMultipliers.Length; i++)
        {
            var sideOffset = BlockSideMultipliers[i];
            var offsetPos = new Vector3<double>(
                pos.X + sideOffset.X,
                pos.Y + sideOffset.Y,
                pos.Z + sideOffset.Z
            );
            possibleRotation = ReachableOffset(ctx, pos, offsetPos, blockReachDistance, wouldSneak);
            if (possibleRotation != null)
            {
                return possibleRotation;
            }
        }
        return null;
    }

    /// <summary>
    /// Determines if the specified entity is able to reach the specified block with
    /// the given offsetted position.
    /// </summary>
    public static Rotation? ReachableOffset(IPlayerContext ctx, BetterBlockPos pos, Vector3<double> offsetPos, double blockReachDistance, bool wouldSneak)
    {
        var player = ctx.Player() as Entity;
        if (player == null) return null;

        Vector3<double> eyes = wouldSneak ? RayTraceUtils.InferSneakingEyePosition(player) : player.EyePosition;
        var currentRot = ctx.PlayerRotations();
        if (currentRot == null) return null;
        Rotation rotation = CalcRotationFromVec3d(eyes, offsetPos, currentRot);
        
        // Get baritone instance - for now use primary baritone
        // TODO: Get baritone for specific player when GetBaritoneForPlayer is available
        var baritone = BaritoneAPI.GetProvider().GetPrimaryBaritone();
        Rotation actualRotation = baritone.GetLookBehavior().GetAimProcessor().PeekRotation(rotation);
        
        var world = ctx.World() as Level;
        var result = RayTraceUtils.RayTraceTowards(player, world, actualRotation, blockReachDistance, wouldSneak);
        if (result != null && result.Block != null)
        {
            if (result.BlockPosition.X == pos.X && result.BlockPosition.Y == pos.Y && result.BlockPosition.Z == pos.Z)
            {
                return rotation;
            }
            // Check for fire block special case
            if (world != null)
            {
                var blockState = world.GetBlockAt(pos.X, pos.Y, pos.Z);
                if (blockState != null && blockState.Name.Contains("fire", StringComparison.OrdinalIgnoreCase))
                {
                    var belowPos = new Vector3<int>(pos.X, pos.Y - 1, pos.Z);
                    if (result.BlockPosition.X == belowPos.X && result.BlockPosition.Y == belowPos.Y && result.BlockPosition.Z == belowPos.Z)
                    {
                        return rotation;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Determines if the specified entity is able to reach the specified block where it is
    /// looking at the direct center of it's hitbox.
    /// </summary>
    public static Rotation? ReachableCenter(IPlayerContext ctx, BetterBlockPos pos, double blockReachDistance, bool wouldSneak)
    {
        var world = ctx.World() as Level;
        if (world == null) return null;
        return ReachableOffset(ctx, pos, VecUtils.GetBlockPosCenter(pos), blockReachDistance, wouldSneak);
    }
}

