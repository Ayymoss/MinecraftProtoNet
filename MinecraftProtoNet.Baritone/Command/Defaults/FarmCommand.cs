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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/FarmCommand.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Command;
using MinecraftProtoNet.Baritone.Api.Command.Argument;

namespace MinecraftProtoNet.Baritone.Command.Defaults;

/// <summary>
/// Farm command implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/FarmCommand.java
/// </summary>
public class FarmCommand : ICommand
{
    private readonly IBaritone _baritone;

    public FarmCommand(IBaritone baritone)
    {
        _baritone = baritone;
    }

    public void Execute(string label, IArgConsumer args)
    {
        int range = 64;
        if (args.HasAny())
        {
            var peekValue = args.Peek().GetValue();
            if (int.TryParse(peekValue, out var r))
            {
                range = r;
                args.Get();
            }
        }

        _baritone.GetFarmProcess().Farm(range);
        _baritone.GetGameEventHandler().LogDirect($"Farming within {range} blocks");
    }

    public IEnumerable<string> TabComplete(string label, IArgConsumer args)
    {
        return Array.Empty<string>();
    }

    public string GetShortDesc()
    {
        return "Farm nearby crops";
    }

    public IReadOnlyList<string> GetLongDesc()
    {
        return new List<string>
        {
            "The farm command tells Baritone to farm nearby crops.",
            "",
            "Usage:",
            "> farm - Farm within 64 blocks",
            "> farm <range> - Farm within specified range"
        };
    }

    public IReadOnlyList<string> GetNames()
    {
        return new List<string> { "farm" };
    }

    public bool HiddenFromHelp()
    {
        return false;
    }
}

