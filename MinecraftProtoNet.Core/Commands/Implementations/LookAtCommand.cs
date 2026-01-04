using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Enums;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("lookat", Description = "Look at coordinates")]
public class LookAtCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
