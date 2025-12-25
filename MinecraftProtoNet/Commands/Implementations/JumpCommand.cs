using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("jump", Description = "Toggle jumping")]
public class JumpCommand : ICommand
{
    public string Name => "jump";
    public string Description => "Toggle jumping";
    public string[] Aliases => [];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
