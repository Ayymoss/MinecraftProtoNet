using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Core.Abstractions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("gotopath", Description = "Pathfind to coordinates")]
public class GotoPathCommand : ICommand
{
    public string Name => "gotopath";
    public string Description => "Pathfind to coordinates (x y z)";
    public string[] Aliases => ["path"];

    private readonly IPathFollowerService _pathFollowerService = new PathFollowerService();

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.TryGetArg(0, out float x) ||
            !ctx.TryGetArg(1, out float y) ||
            !ctx.TryGetArg(2, out float z))
        {
            await ctx.SendUnsignedChatAsync("Usage: !gotopath <x> <y> <z>");
            return;
        }

        var result = MovementActions.PathfindTo(ctx, new Vector3<double>(x, y, z), _pathFollowerService);

        if (!result)
        {
            await ctx.SendUnsignedChatAsync("I can't reach that position.");
            return;
        }

        await ctx.SendUnsignedChatAsync("Moving to that position.");
    }
}
