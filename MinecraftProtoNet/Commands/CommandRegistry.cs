using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;

namespace MinecraftProtoNet.Commands;

/// <summary>
/// Registry for command discovery, registration, and execution.
/// </summary>
public class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<CommandRegistry> _logger = LoggingConfiguration.CreateLogger<CommandRegistry>();

    /// <summary>
    /// Registers a command instance using metadata from its CommandAttribute.
    /// </summary>
    public void RegisterCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var attribute = command.GetType().GetCustomAttribute<CommandAttribute>()
                       ?? throw new InvalidOperationException($"Command type {command.GetType().Name} is missing CommandAttribute.");

        _commands[attribute.Name] = command;
        foreach (var alias in attribute.Aliases)
        {
            _commands[alias] = command;
        }
    }

    /// <summary>
    /// Auto-discovers and registers all commands in the specified assembly.
    /// </summary>
    public void AutoRegisterCommands(IServiceProvider serviceProvider, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();
        var logger = serviceProvider.GetRequiredService<ILogger<CommandRegistry>>();
 
        var commandTypes = assembly.GetTypes()
            .Where(t => typeof(ICommand).IsAssignableFrom(t)
                        && t is { IsClass: true, IsAbstract: false }
                        && t.GetCustomAttribute<CommandAttribute>() != null);
 
        foreach (var type in commandTypes)
        {
            try
            {
                if (ActivatorUtilities.CreateInstance(serviceProvider, type) is ICommand command)
                {
                    RegisterCommand(command);
                    logger.LogDebug("Registered command: {CommandType}", type.Name);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to register command {CommandType}", type.Name);
            }
        }
    }

    /// <summary>
    /// Gets a command by name or alias.
    /// </summary>
    public ICommand? GetCommand(string name)
    {
        return _commands.GetValueOrDefault(name);
    }

    /// <summary>
    /// Gets all registered commands (unique, no aliases).
    /// </summary>
    public IEnumerable<ICommand> GetAllCommands()
    {
        return _commands.Values.Distinct();
    }

    /// <summary>
    /// Executes a command by name with the given context.
    /// </summary>
    public async Task<bool> ExecuteAsync(string commandName, CommandContext context)
    {
        var command = GetCommand(commandName);
        if (command == null)
        {
            await context.SendChatAsync($"Unknown command: {commandName}");
            return false;
        }

        try
        {
            await command.ExecuteAsync(context);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command '{CommandName}' failed", commandName);
            return false;
        }
    }

    /// <summary>
    /// Gets all commands that can be executed from external sources (web UI, API, etc.).
    /// Excludes commands that require in-game player context.
    /// </summary>
    public IEnumerable<(string Name, string Description, ICommand Command)> GetExternalCommands()
    {
        return _commands.Values
            .Distinct()
            .Select(cmd =>
            {
                var attr = cmd.GetType().GetCustomAttribute<CommandAttribute>()!;
                return (attr.Name, attr.Description, cmd, attr.PlayerContextRequired);
            })
            .Where(x => !x.PlayerContextRequired)
            .Select(x => (x.Name, x.Description, x.cmd));
    }

    /// <summary>
    /// Executes a command from an external source (no in-game player context).
    /// Returns error message if command requires player context or fails.
    /// </summary>
    public async Task<(bool Success, string Message)> ExecuteExternalAsync(
        string commandName, 
        string[] args,
        IMinecraftClient client)
    {
        var command = GetCommand(commandName);
        if (command == null)
        {
            return (false, $"Unknown command: {commandName}");
        }

        var attr = command.GetType().GetCustomAttribute<CommandAttribute>();
        if (attr?.PlayerContextRequired == true)
        {
            return (false, $"Command '{commandName}' requires in-game player context");
        }

        try
        {
            // Create a context with no sender (external execution)
            var context = new CommandContext(client, client.State, client.AuthResult, Guid.Empty, args);
            await command.ExecuteAsync(context);
            return (true, $"Command '{commandName}' executed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External command '{CommandName}' failed", commandName);
            return (false, $"Command failed: {ex.Message}");
        }
    }
}

