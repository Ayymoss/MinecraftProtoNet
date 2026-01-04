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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/PathCalculationResult.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Calc;

namespace MinecraftProtoNet.Baritone.Api.Utils;

/// <summary>
/// Result of a path calculation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/PathCalculationResult.java
/// </summary>
public class PathCalculationResult
{
    private readonly IPath? _path;
    private readonly PathCalculationResultType _type;

    public PathCalculationResult(PathCalculationResultType type)
        : this(type, null)
    {
    }

    public PathCalculationResult(PathCalculationResultType type, IPath? path)
    {
        _type = type;
        _path = path;
    }

    public IPath? GetPath() => _path;

    public new PathCalculationResultType GetType() => _type;

    public enum PathCalculationResultType
    {
        SuccessToGoal,
        SuccessSegment,
        Failure,
        Cancellation,
        Exception
    }
}

