using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Core.Abstractions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("here", Description = "Pathfind to sender's position")]
public class HereCommand : ICommand
{
    public string Name => "here";
    public string Description => "Pathfind to the sender's position";
    public string[] Aliases => ["come"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (ctx.Sender?.HasEntity != true)
        {
            await ctx.SendChatAsync("I don't know where you are!");
            return;
        }

        var targetPosition = ctx.Sender.Entity.Position;
        await ctx.SendChatAsync("On my way!");
        
        var result = MovementActions.PathfindTo(ctx, targetPosition);

        if (!result)
        {
            await ctx.SendChatAsync("I can't get any closer to your position.");
        }
    }
}
