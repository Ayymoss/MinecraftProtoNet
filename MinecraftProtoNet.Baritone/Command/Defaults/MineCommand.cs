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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/MineCommand.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Command;
using MinecraftProtoNet.Baritone.Api.Command.Argument;
using MinecraftProtoNet.Baritone.Utils;

namespace MinecraftProtoNet.Baritone.Command.Defaults;

/// <summary>
/// Mine command implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/MineCommand.java
/// </summary>
public class MineCommand : ICommand
{
    private readonly IBaritone _baritone;

    public MineCommand(IBaritone baritone)
    {
        _baritone = baritone;
    }

    public void Execute(string label, IArgConsumer args)
    {
        // Parse quantity (optional first argument)
        int quantity = 0;
        if (args.HasAny())
        {
            var peekValue = args.Peek().GetValue();
            if (int.TryParse(peekValue, out var qty))
            {
                quantity = qty;
                args.Get();
            }
        }

        args.RequireMin(1);
        
        var blocks = new List<string>();
        while (args.HasAny())
        {
            blocks.Add(args.GetString());
        }

        if (blocks.Count == 0)
        {
            _baritone.GetGameEventHandler().LogDirect("Usage: mine [quantity] <block1> [block2] ...");
            return;
        }

        _baritone.GetGameEventHandler().LogDirect($"Mining {string.Join(", ", blocks)}");
        if (quantity > 0)
        {
            _baritone.GetMineProcess().MineByName(quantity, blocks.ToArray());
        }
        else
        {
            _baritone.GetMineProcess().MineByName(blocks.ToArray());
        }
    }

    public IEnumerable<string> TabComplete(string label, IArgConsumer args)
    {
        // TODO: Implement block name tab completion when block registry is available
        return Array.Empty<string>();
    }

    public string GetShortDesc()
    {
        return "Mine some blocks";
    }

    public IReadOnlyList<string> GetLongDesc()
    {
        return new List<string>
        {
            "The mine command allows you to tell Baritone to search for and mine individual blocks.",
            "",
            "Usage:",
            "> mine diamond_ore - Mines all diamonds it can find.",
            "> mine 64 iron_ore - Mines 64 iron ore blocks."
        };
    }

    public IReadOnlyList<string> GetNames()
    {
        return new List<string> { "mine", "gather" };
    }

    public bool HiddenFromHelp()
    {
        return false;
    }
}

