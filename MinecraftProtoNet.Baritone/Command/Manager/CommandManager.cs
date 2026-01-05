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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/manager/CommandManager.java
 */

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Command;
using MinecraftProtoNet.Baritone.Api.Command.Argument;
using MinecraftProtoNet.Baritone.Api.Command.Manager;
using MinecraftProtoNet.Baritone.Command.Argument;
using MinecraftProtoNet.Baritone.Command.Defaults;
using MinecraftProtoNet.Core.Core;

namespace MinecraftProtoNet.Baritone.Command.Manager;

/// <summary>
/// Command manager implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/manager/CommandManager.java
/// </summary>
public class CommandManager : ICommandManager
{
    private readonly IBaritone _baritone;
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public CommandManager(IBaritone baritone)
    {
        _baritone = baritone;
        // Register default commands
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/defaults/DefaultCommands.java:30-78
        var commands = DefaultCommands.CreateAll(baritone);
        foreach (var command in commands)
        {
            Register(command);
        }
    }

    public IBaritone GetBaritone() => _baritone;

    public ICommand? GetCommand(string name)
    {
        return _commands.TryGetValue(name.ToLowerInvariant(), out var command) ? command : null;
    }

    public void Execute(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var expanded = Expand(command);
        Execute(expanded);
    }

    private void Execute((string Label, List<string> Args) expanded)
    {
        var command = GetCommand(expanded.Label);
        if (command == null)
        {
            // Command not found - log and inform user
            var logger = LoggingConfiguration.CreateLogger<CommandManager>();
            logger.LogWarning("Baritone command '{Command}' not found. Available commands: {Commands}", 
                expanded.Label, string.Join(", ", _commands.Keys));
            _baritone.GetGameEventHandler().LogDirect($"Command '{expanded.Label}' not found. Use 'help' to see available commands.");
            return;
        }

        try
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/manager/CommandManager.java:71-77
            // Use ArgConsumer for proper argument parsing
            var argConsumer = new ArgConsumer(this, expanded.Args);
            command.Execute(expanded.Label, argConsumer);
        }
        catch (Exception e)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/manager/CommandManager.java:77-79
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/Helper.java:239-244
            // Handle command exceptions
            // In Java, this uses CommandException.handle(command, args.getArgs())
            // Log the full exception with stack trace to the logging infrastructure
            var logger = LoggingConfiguration.CreateLogger<CommandManager>();
            logger.LogError(e, "Baritone command exception executing '{Command}': {Message}", expanded.Label, e.Message);
            
            // Also log via LogDirect (which will use the configured logger)
            _baritone.GetGameEventHandler().LogDirect($"Command exception: {e.Message}");
        }
    }

    public IReadOnlyList<string> TabComplete(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return _commands.Keys.ToList();
        }

        var expanded = Expand(command, true);
        if (expanded.Args.Count == 0)
        {
            // Tab complete command names
            var prefix = expanded.Label.ToLowerInvariant();
            return _commands.Keys
                .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/manager/CommandManager.java:99-105
        // Tab complete command arguments
        var cmd = GetCommand(expanded.Label);
        if (cmd != null)
        {
            var argConsumer = new ArgConsumer(this, expanded.Args);
            return cmd.TabComplete(expanded.Label, argConsumer).ToList();
        }
        return Array.Empty<string>();
    }

    public void Register(ICommand command)
    {
        foreach (var name in command.GetNames())
        {
            _commands[name.ToLowerInvariant()] = command;
        }
    }

    private static (string Label, List<string> Args) Expand(string command, bool preserveEmptyLast = false)
    {
        var parts = Regex.Split(command.Trim(), @"\s+", RegexOptions.Compiled);
        if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
        {
            return ("", new List<string>());
        }

        var label = parts[0];
        var args = parts.Skip(1).ToList();

        if (!preserveEmptyLast && args.Count > 0 && string.IsNullOrEmpty(args[^1]))
        {
            args.RemoveAt(args.Count - 1);
        }

        return (label, args);
    }
}

