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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/command/ICommand.java
 */

using MinecraftProtoNet.Baritone.Api.Command.Argument;

namespace MinecraftProtoNet.Baritone.Api.Command;

/// <summary>
/// The base for a command.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/command/ICommand.java
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Called when this command is executed.
    /// </summary>
    void Execute(string label, IArgConsumer args);

    /// <summary>
    /// Called when the command needs to tab complete.
    /// </summary>
    IEnumerable<string> TabComplete(string label, IArgConsumer args);

    /// <summary>
    /// Gets a single-line string containing a short description of this command's purpose.
    /// </summary>
    string GetShortDesc();

    /// <summary>
    /// Gets a list of lines that will be printed by the help command.
    /// </summary>
    IReadOnlyList<string> GetLongDesc();

    /// <summary>
    /// Gets a list of the names that can be accepted to have arguments passed to this command.
    /// </summary>
    IReadOnlyList<string> GetNames();

    /// <summary>
    /// Returns true if this command should be hidden from the help menu.
    /// </summary>
    bool HiddenFromHelp();
}

