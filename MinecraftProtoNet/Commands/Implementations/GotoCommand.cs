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
        if (!ctx.TryGetArg(0, out float x) ||
            !ctx.TryGetArg(1, out float y) ||
            !ctx.TryGetArg(2, out float z))
        {
            await ctx.SendChatAsync("Usage: !goto <x> <y> <z> [speed]");
            return;
        }

        ctx.TryGetArg(3, out float speed);
        speed = speed > 0 ? speed : 0.25f;

        await MovementActions.MoveToAsync(ctx, new Vector3<double>(x, y, z), speed);
        await ctx.SendChatAsync($"Moving to {x:N2}, {y:N2}, {z:N2}");
    }
}
