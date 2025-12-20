using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Core.Abstractions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("here", Description = "Pathfind to sender's position")]
public class HereCommand : ICommand
{
    public string Name => "here";
    public string Description => "Pathfind to the sender's position";
    public string[] Aliases => ["come"];

    public Task ExecuteAsync(CommandContext ctx)
    {
        if (ctx.Sender?.HasEntity != true)
        {
            Console.WriteLine("Sender position not available.");
            return Task.CompletedTask;
        }

        var targetPosition = ctx.Sender.Entity.Position;
        var result = MovementActions.PathfindTo(ctx, targetPosition);

        if (!result)
        {
            Console.WriteLine("I can't reach your position.");
        }

        return Task.CompletedTask;
    }
}
