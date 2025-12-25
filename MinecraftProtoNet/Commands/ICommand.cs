namespace MinecraftProtoNet.Commands;

/// <summary>
/// Interface for chat-triggered commands.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// The primary name of the command (without the ! prefix).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// A brief description of what the command does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Alternative names for the command.
    /// </summary>
    string[] Aliases { get; }

    /// <summary>
    /// Executes the command with the given context.
    /// </summary>
    Task ExecuteAsync(CommandContext context);
}
