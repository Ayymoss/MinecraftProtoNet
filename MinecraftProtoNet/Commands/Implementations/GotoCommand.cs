using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Models.Core;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("goto", Description = "Move to coordinates")]
public class GotoCommand : ICommand
{
    public string Name => "goto";
    public string Description => "Move to coordinates (x y z [speed])";
    public string[] Aliases => ["move"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
