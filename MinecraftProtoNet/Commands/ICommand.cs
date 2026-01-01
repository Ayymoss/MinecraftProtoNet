namespace MinecraftProtoNet.Commands;

/// <summary>
/// Interface for chat-triggered commands.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Executes the command with the given context.
    /// </summary>
    Task ExecuteAsync(CommandContext context);
}
