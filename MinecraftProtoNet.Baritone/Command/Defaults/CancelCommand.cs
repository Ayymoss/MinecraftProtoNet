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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/ExecutionControlCommands.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Command;
using MinecraftProtoNet.Baritone.Api.Command.Argument;
using MinecraftProtoNet.Baritone.Process;

namespace MinecraftProtoNet.Baritone.Command.Defaults;

/// <summary>
/// Cancel command implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/ExecutionControlCommands.java
/// </summary>
public class CancelCommand : ICommand
{
    private readonly IBaritone _baritone;

    public CancelCommand(IBaritone baritone)
    {
        _baritone = baritone;
    }

    public void Execute(string label, IArgConsumer args)
    {
        // Cancel all active processes
        _baritone.GetCustomGoalProcess().SetGoal(null!);
        _baritone.GetMineProcess().Cancel();
        _baritone.GetFollowProcess().Cancel();
        // FarmProcess has Cancel() but it's not in the interface - call it via the concrete type
        if (_baritone.GetFarmProcess() is FarmProcess farmProcess)
        {
            farmProcess.Cancel();
        }
        _baritone.GetGameEventHandler().LogDirect("Cancelled all processes");
    }

    public IEnumerable<string> TabComplete(string label, IArgConsumer args)
    {
        return Array.Empty<string>();
    }

    public string GetShortDesc()
    {
        return "Cancel the current process";
    }

    public IReadOnlyList<string> GetLongDesc()
    {
        return new List<string>
        {
            "The cancel command cancels all active Baritone processes.",
            "",
            "Usage:",
            "> cancel - Cancel all active processes"
        };
    }

    public IReadOnlyList<string> GetNames()
    {
        return new List<string> { "cancel", "stop" };
    }

    public bool HiddenFromHelp()
    {
        return false;
    }
}

