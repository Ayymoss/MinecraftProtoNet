using System.Reflection;

namespace MinecraftProtoNet.Commands;

/// <summary>
/// Registry for command discovery, registration, and execution.
/// </summary>
public class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a command instance.
    /// </summary>
    public void RegisterCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        _commands[command.Name] = command;
        foreach (var alias in command.Aliases)
        {
            _commands[alias] = command;
        }
    }

    /// <summary>
    /// Auto-discovers and registers all commands in the specified assembly.
    /// If no assembly is provided, uses the calling assembly.
    /// </summary>
    public void AutoRegisterCommands(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();

        var commandTypes = assembly.GetTypes()
            .Where(t => typeof(ICommand).IsAssignableFrom(t)
                        && t is { IsClass: true, IsAbstract: false }
                        && t.GetCustomAttribute<CommandAttribute>() != null);

        foreach (var type in commandTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is ICommand command)
                {
                    RegisterCommand(command);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Failed to register command {type.Name}: {ex.Message}");
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
            Console.WriteLine($"[ERROR] Command '{commandName}' failed: {ex.Message}");
            return false;
        }
    }
}
