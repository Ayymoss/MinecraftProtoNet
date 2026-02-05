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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/DefaultCommands.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Command;

namespace MinecraftProtoNet.Baritone.Command.Defaults;

/// <summary>
/// Creates all default Baritone commands.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/DefaultCommands.java
/// </summary>
public static class DefaultCommands
{
    /// <summary>
    /// Creates all default commands for the given Baritone instance.
    /// </summary>
    public static List<ICommand> CreateAll(IBaritone baritone)
    {
        if (baritone == null)
        {
            throw new ArgumentNullException(nameof(baritone));
        }

        return new List<ICommand>
        {
            // Core commands
            new GoalCommand(baritone),
            new GotoCommand(baritone),
            new MineCommand(baritone),
            new FollowCommand(baritone),
            new FarmCommand(baritone),
            new CancelCommand(baritone),
            // TODO: Add remaining commands (help, set, path, proc, explore, etc.)
        };
    }
}

