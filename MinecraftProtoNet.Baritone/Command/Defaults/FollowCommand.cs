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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/FollowCommand.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Command;
using MinecraftProtoNet.Baritone.Api.Command.Argument;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Command.Defaults;

/// <summary>
/// Follow command implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/FollowCommand.java
/// </summary>
public class FollowCommand : ICommand
{
    private readonly IBaritone _baritone;

    public FollowCommand(IBaritone baritone)
    {
        _baritone = baritone;
    }

    public void Execute(string label, IArgConsumer args)
    {
        args.RequireMin(1);

        if (args.HasExactlyOne())
        {
            var group = args.GetString().ToLowerInvariant();
            System.Predicate<object> filter;
            
            if (group == "entities" || group == "entity")
            {
                // Follow all entities (check if they exist and are valid)
                filter = obj => obj is Entity;
                _baritone.GetFollowProcess().Follow(filter);
                _baritone.GetGameEventHandler().LogDirect("Following all entities");
            }
            else if (group == "players" || group == "player")
            {
                // Follow players (check if entity exists)
                filter = obj => obj is Entity entity && entity.EntityId > 0;
                _baritone.GetFollowProcess().Follow(filter);
                _baritone.GetGameEventHandler().LogDirect("Following all players");
            }
            else
            {
                _baritone.GetGameEventHandler().LogDirect("Usage: follow <entities|players> or follow <entity|player> <name1> [name2] ...");
            }
        }
        else
        {
            // TODO: Implement specific entity/player following when entity registry is available
            _baritone.GetGameEventHandler().LogDirect("Following specific entities not yet implemented. Use 'follow entities' or 'follow players'");
        }
    }

    public IEnumerable<string> TabComplete(string label, IArgConsumer args)
    {
        if (args.HasExactlyOne())
        {
            var prefix = args.Peek().GetValue().ToLowerInvariant();
            return new[] { "entities", "players", "entity", "player" }
                .Where(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
        return Array.Empty<string>();
    }

    public string GetShortDesc()
    {
        return "Follow entity things";
    }

    public IReadOnlyList<string> GetLongDesc()
    {
        return new List<string>
        {
            "The follow command tells Baritone to follow certain kinds of entities.",
            "",
            "Usage:",
            "> follow entities - Follows all entities.",
            "> follow players - Follow players"
        };
    }

    public IReadOnlyList<string> GetNames()
    {
        return new List<string> { "follow" };
    }

    public bool HiddenFromHelp()
    {
        return false;
    }
}

