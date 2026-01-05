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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/GotoCommand.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Command;
using MinecraftProtoNet.Baritone.Api.Command.Argument;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Command.Defaults;

/// <summary>
/// Goto command implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/GotoCommand.java
/// </summary>
public class GotoCommand : ICommand
{
    private readonly IBaritone _baritone;
    private readonly IPlayerContext _ctx;

    public GotoCommand(IBaritone baritone)
    {
        _baritone = baritone;
        _ctx = baritone.GetPlayerContext();
    }

    public void Execute(string label, IArgConsumer args)
    {
        // Parse coordinates: goto <x> <y> <z> or goto <x> <z> or goto <y>
        // For now, we'll handle the simple case of 3 coordinates
        args.RequireMax(3);
        
        if (args.HasAny())
        {
            // Try to parse as coordinates
            var coords = new List<double>();
            while (args.HasAny() && coords.Count < 3)
            {
                var peekValue = args.Peek().GetValue();
                if (double.TryParse(peekValue, out var coord))
                {
                    coords.Add(coord);
                    args.Get();
                }
                else
                {
                    break;
                }
            }

            if (coords.Count == 3)
            {
                // goto <x> <y> <z>
                var goal = new GoalBlock((int)coords[0], (int)coords[1], (int)coords[2]);
                _baritone.GetGameEventHandler().LogDirect($"Going to: {goal}");
                _baritone.GetCustomGoalProcess().SetGoalAndPath(goal);
            }
            else if (coords.Count == 2)
            {
                // goto <x> <z> - use current Y
                var playerFeet = _ctx.PlayerFeet();
                var goal = new GoalXZ((int)coords[0], (int)coords[1]);
                _baritone.GetGameEventHandler().LogDirect($"Going to: {goal}");
                _baritone.GetCustomGoalProcess().SetGoalAndPath(goal);
            }
            else if (coords.Count == 1)
            {
                // goto <y> - use current X and Z
                var playerFeet = _ctx.PlayerFeet();
                var goal = new GoalYLevel((int)coords[0]);
                _baritone.GetGameEventHandler().LogDirect($"Going to: {goal}");
                _baritone.GetCustomGoalProcess().SetGoalAndPath(goal);
            }
            else
            {
                _baritone.GetGameEventHandler().LogDirect("Usage: goto <x> <y> <z> or goto <x> <z> or goto <y>");
            }
        }
        else
        {
            _baritone.GetGameEventHandler().LogDirect("Usage: goto <x> <y> <z> or goto <x> <z> or goto <y>");
        }
    }

    public IEnumerable<string> TabComplete(string label, IArgConsumer args)
    {
        // No tab completion for now
        return Array.Empty<string>();
    }

    public string GetShortDesc()
    {
        return "Go to a coordinate or block";
    }

    public IReadOnlyList<string> GetLongDesc()
    {
        return new List<string>
        {
            "The goto command tells Baritone to head towards a given goal or block.",
            "",
            "Usage:",
            "> goto <y> - Go to a Y level",
            "> goto <x> <z> - Go to an X,Z position",
            "> goto <x> <y> <z> - Go to an X,Y,Z position"
        };
    }

    public IReadOnlyList<string> GetNames()
    {
        return new List<string> { "goto" };
    }

    public bool HiddenFromHelp()
    {
        return false;
    }
}

