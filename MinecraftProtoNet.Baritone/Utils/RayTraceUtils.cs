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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/RayTraceUtils.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Models.World.Meta;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Utility class for ray tracing.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/RayTraceUtils.java
/// </summary>
public static class RayTraceUtils
{
    /// <summary>
    /// Performs a block raytrace with the specified rotations.
    /// </summary>
    public static RaycastHit? RayTraceTowards(Entity entity, Rotation rotation, double blockReachDistance)
    {
        return RayTraceTowards(entity, rotation, blockReachDistance, false);
    }

    /// <summary>
    /// Performs a block raytrace with the specified rotations.
    /// </summary>
    public static RaycastHit? RayTraceTowards(Entity entity, Rotation rotation, double blockReachDistance, bool wouldSneak)
    {
        return RayTraceTowards(entity, null, rotation, blockReachDistance, wouldSneak);
    }

    /// <summary>
    /// Performs a block raytrace with the specified rotations.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/RayTraceUtils.java:48-63
    /// </summary>
    public static RaycastHit? RayTraceTowards(Entity entity, Level? level, Rotation rotation, double blockReachDistance, bool wouldSneak)
    {
        Vector3<double> start;
        if (wouldSneak)
        {
            start = InferSneakingEyePosition(entity);
        }
        else
        {
            start = entity.EyePosition;
        }

        Vector3<double> direction = RotationUtils.CalcLookDirectionFromRotation(rotation);
        Vector3<double> end = new Vector3<double>(
            start.X + direction.X * blockReachDistance,
            start.Y + direction.Y * blockReachDistance,
            start.Z + direction.Z * blockReachDistance
        );

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/RayTraceUtils.java:62
        // The Java version uses entity.level().clip() which performs a block-only raytrace
        if (level != null)
        {
            return level.RayCast(start, direction, blockReachDistance);
        }
        return null;
    }

    /// <summary>
    /// Infers the eye position when the entity is sneaking.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/RayTraceUtils.java:65-67
    /// </summary>
    public static Vector3<double> InferSneakingEyePosition(Entity entity)
    {
        // When sneaking, eye height is reduced
        // Reference: Minecraft's Entity.getEyeHeight(Pose.CROUCHING)
        // Crouching eye height is typically 1.27 (compared to standing 1.62)
        const double crouchingEyeHeight = 1.27;
        return new Vector3<double>(
            entity.Position.X,
            entity.Position.Y + crouchingEyeHeight,
            entity.Position.Z
        );
    }
}

