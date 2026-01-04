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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/command/argument/ICommandArgument.java
 */

namespace MinecraftProtoNet.Baritone.Api.Command.Argument;

/// <summary>
/// Interface for command argument.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/command/argument/ICommandArgument.java
/// </summary>
public interface ICommandArgument
{
    /// <summary>
    /// Gets the value of this argument.
    /// </summary>
    string GetValue();

    /// <summary>
    /// Gets the index of this argument in the original command string.
    /// </summary>
    int GetIndex();

    /// <summary>
    /// Gets an enum value from this argument.
    /// </summary>
    T GetEnum<T>() where T : struct, Enum;
}

