using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Core.Abstractions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("gotopath", Description = "Pathfind to coordinates")]
public class GotoPathCommand : ICommand
{
    public string Name => "gotopath";
    public string Description => "Pathfind to coordinates (x y z)";
    public string[] Aliases => ["path"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
