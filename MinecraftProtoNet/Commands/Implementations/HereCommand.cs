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
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
