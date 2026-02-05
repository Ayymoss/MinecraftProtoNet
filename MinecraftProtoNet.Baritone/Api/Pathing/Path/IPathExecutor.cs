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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/path/IPathExecutor.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Calc;

namespace MinecraftProtoNet.Baritone.Api.Pathing.Path;

/// <summary>
/// Interface for path executor.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/path/IPathExecutor.java
/// </summary>
public interface IPathExecutor
{
    /// <summary>
    /// Gets the path being executed.
    /// </summary>
    IPath GetPath();

    /// <summary>
    /// Gets the current position in the path.
    /// </summary>
    int GetPosition();

    /// <summary>
    /// Checks if the path execution has finished.
    /// </summary>
    bool Finished();

    /// <summary>
    /// Checks if the path execution has failed.
    /// </summary>
    bool Failed();
}

