using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("cmd", Description = "Execute a server command")]
public class CmdCommand : ICommand
{
    public string Name => "cmd";
    public string Description => "Execute a server command";
    public string[] Aliases => ["command"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.HasMinArgs(1))
        {
            await ctx.SendUnsignedChatAsync("Usage: !cmd <command>");
            return;
        }

        var command = ctx.GetRemainingArgsAsString(0);
        await ChatActions.SendCommandAsync(ctx, command);
    }
}
