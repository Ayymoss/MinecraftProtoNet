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
        var isJumping = MovementActions.ToggleJumping(ctx);
        var message = isJumping ? "Jumping!" : "Stopped jumping!";
        await ctx.SendChatAsync(message);
    }
}
