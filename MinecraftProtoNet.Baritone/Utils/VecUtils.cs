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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/VecUtils.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Utility class for vector calculations.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/VecUtils.java
/// </summary>
public static class VecUtils
{
    /// <summary>
    /// Calculates the center of the block at the specified position's bounding box
    /// </summary>
    public static Vector3<double> CalculateBlockCenter(Level world, BetterBlockPos pos)
    {
        var blockState = world.GetBlockAt(pos.X, pos.Y, pos.Z);
        if (blockState == null)
        {
            return GetBlockPosCenter(pos);
        }

        // For now, use simple center calculation
        // Full implementation would use VoxelShape calculations from block collision shape
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/VecUtils.java:45-64
        double xDiff = 0.5;
        double yDiff = 0.5;
        double zDiff = 0.5;

        // Special case for fire blocks - look at bottom
        if (blockState.Name.Contains("fire", StringComparison.OrdinalIgnoreCase))
        {
            yDiff = 0;
        }

        return new Vector3<double>(
            pos.X + xDiff,
            pos.Y + yDiff,
            pos.Z + zDiff
        );
    }

    /// <summary>
    /// Gets the assumed center position of the given block position.
    /// This is done by adding 0.5 to the X, Y, and Z axes.
    /// </summary>
    public static Vector3<double> GetBlockPosCenter(BetterBlockPos pos)
    {
        return new Vector3<double>(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5);
    }

    /// <summary>
    /// Gets the distance from the specified position to the assumed center of the specified block position.
    /// </summary>
    public static double DistanceToCenter(BetterBlockPos pos, double x, double y, double z)
    {
        double xdiff = pos.X + 0.5 - x;
        double ydiff = pos.Y + 0.5 - y;
        double zdiff = pos.Z + 0.5 - z;
        return Math.Sqrt(xdiff * xdiff + ydiff * ydiff + zdiff * zdiff);
    }

    /// <summary>
    /// Gets the distance from the specified entity's position to the assumed
    /// center of the specified block position.
    /// </summary>
    public static double EntityDistanceToCenter(Entity entity, BetterBlockPos pos)
    {
        return DistanceToCenter(pos, entity.Position.X, entity.Position.Y, entity.Position.Z);
    }

    /// <summary>
    /// Gets the distance from the specified entity's position to the assumed
    /// center of the specified block position, ignoring the Y axis.
    /// </summary>
    public static double EntityFlatDistanceToCenter(Entity entity, BetterBlockPos pos)
    {
        return DistanceToCenter(pos, entity.Position.X, pos.Y + 0.5, entity.Position.Z);
    }
}

