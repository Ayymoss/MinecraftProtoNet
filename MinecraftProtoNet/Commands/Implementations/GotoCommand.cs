using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Models.Core;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("goto", Description = "Move to coordinates")]
public class GotoCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
