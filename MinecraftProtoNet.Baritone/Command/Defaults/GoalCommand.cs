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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/GoalCommand.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Command;
using MinecraftProtoNet.Baritone.Api.Command.Argument;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;

namespace MinecraftProtoNet.Baritone.Command.Defaults;

/// <summary>
/// Goal command implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/GoalCommand.java
/// </summary>
public class GoalCommand : ICommand
{
    private readonly IBaritone _baritone;
    private readonly IPlayerContext _ctx;

    public GoalCommand(IBaritone baritone)
    {
        _baritone = baritone;
        _ctx = baritone.GetPlayerContext();
    }

    public void Execute(string label, IArgConsumer args)
    {
        var goalProcess = _baritone.GetCustomGoalProcess();
        
        if (args.HasAny())
        {
            var peekValue = args.Peek().GetValue();
            if (peekValue == "reset" || peekValue == "clear" || peekValue == "none")
            {
                args.RequireMax(1);
                args.Get();
                if (goalProcess.GetGoal() != null)
                {
                    goalProcess.SetGoal(null!);
                    _baritone.GetGameEventHandler().LogDirect("Cleared goal");
                }
                else
                {
                    _baritone.GetGameEventHandler().LogDirect("There was no goal to clear");
                }
                return;
            }
        }

        // Parse coordinates: goal <x> <y> <z> or goal <x> <z> or goal <y>
        args.RequireMax(3);
        
        if (!args.HasAny())
        {
            // Set goal to current position
            var playerFeet = _ctx.PlayerFeet();
            if (playerFeet != null)
            {
                var currentGoal = new GoalBlock(playerFeet);
                goalProcess.SetGoal(currentGoal);
                _baritone.GetGameEventHandler().LogDirect($"Goal: {currentGoal}");
            }
            return;
        }

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

        Goal targetGoal;
        if (coords.Count == 3)
        {
            targetGoal = new GoalBlock((int)coords[0], (int)coords[1], (int)coords[2]);
        }
        else if (coords.Count == 2)
        {
            targetGoal = new GoalXZ((int)coords[0], (int)coords[1]);
        }
        else if (coords.Count == 1)
        {
            targetGoal = new GoalYLevel((int)coords[0]);
        }
        else
        {
            _baritone.GetGameEventHandler().LogDirect("Usage: goal [reset/clear/none] or goal <x> <y> <z> or goal <x> <z> or goal <y>");
            return;
        }

        goalProcess.SetGoal(targetGoal);
        _baritone.GetGameEventHandler().LogDirect($"Goal: {targetGoal}");
    }

    public IEnumerable<string> TabComplete(string label, IArgConsumer args)
    {
        if (args.HasExactlyOne())
        {
            var prefix = args.Peek().GetValue().ToLowerInvariant();
            return new[] { "reset", "clear", "none", "~" }
                .Where(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
        return Array.Empty<string>();
    }

    public string GetShortDesc()
    {
        return "Set or clear the goal";
    }

    public IReadOnlyList<string> GetLongDesc()
    {
        return new List<string>
        {
            "The goal command allows you to set or clear Baritone's goal.",
            "",
            "Usage:",
            "> goal - Set the goal to your current position",
            "> goal <reset/clear/none> - Erase the goal",
            "> goal <y> - Set the goal to a Y level",
            "> goal <x> <z> - Set the goal to an X,Z position",
            "> goal <x> <y> <z> - Set the goal to an X,Y,Z position"
        };
    }

    public IReadOnlyList<string> GetNames()
    {
        return new List<string> { "goal" };
    }

    public bool HiddenFromHelp()
    {
        return false;
    }
}

