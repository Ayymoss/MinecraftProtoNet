using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("sneak", Description = "Toggle sneaking")]
public class SneakCommand : ICommand
{
    public string Name => "sneak";
    public string Description => "Toggle sneaking";
    public string[] Aliases => ["crouch"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
