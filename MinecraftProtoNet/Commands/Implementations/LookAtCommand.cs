using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Enums;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("lookat", Description = "Look at coordinates")]
public class LookAtCommand : ICommand
{
    public string Name => "lookat";
    public string Description => "Look at coordinates (x y z [face])";
    public string[] Aliases => ["look"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
