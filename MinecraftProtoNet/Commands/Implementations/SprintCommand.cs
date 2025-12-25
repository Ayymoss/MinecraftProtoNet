using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("sprint", Description = "Toggle sprinting")]
public class SprintCommand : ICommand
{
    public string Name => "sprint";
    public string Description => "Toggle sprinting";
    public string[] Aliases => ["run"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
