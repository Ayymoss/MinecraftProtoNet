using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("forward", Description = "Toggle forward movement")]
public class ForwardCommand : ICommand
{
    public string Name => "forward";
    public string Description => "Toggle forward movement";
    public string[] Aliases => [];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
