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
        if (!ctx.TryGetArg(0, out float x) ||
            !ctx.TryGetArg(1, out float y) ||
            !ctx.TryGetArg(2, out float z))
        {
            await ctx.SendChatAsync("Usage: !gotopath <x> <y> <z>");
            return;
        }

        var result = MovementActions.PathfindTo(ctx, new Vector3<double>(x, y, z));

        if (!result)
        {
            await ctx.SendChatAsync("I can't reach that position.");
            return;
        }

        await ctx.SendChatAsync("Moving to that position.");
    }
}
