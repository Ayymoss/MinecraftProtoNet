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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/PathingCommand.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;

namespace MinecraftProtoNet.Baritone.Api.Process;

/// <summary>
/// Command for pathing behavior.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/PathingCommand.java
/// </summary>
public class PathingCommand
{
    /// <summary>
    /// The target goal, may be null.
    /// </summary>
    public readonly Goal? Goal;

    /// <summary>
    /// The command type.
    /// </summary>
    public readonly PathingCommandType CommandType;

    /// <summary>
    /// Create a new PathingCommand.
    /// </summary>
    /// <param name="goal">The target goal, may be null.</param>
    /// <param name="commandType">The command type, cannot be null.</param>
    public PathingCommand(Goal? goal, PathingCommandType commandType)
    {
        CommandType = commandType;
        Goal = goal;
    }

    public override string ToString()
    {
        return $"{CommandType} {Goal}";
    }
}

