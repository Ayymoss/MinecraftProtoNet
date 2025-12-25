using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("jump", Description = "Toggle jumping")]
public class JumpCommand : ICommand
{
    public string Name => "jump";
    public string Description => "Toggle jumping (on/off or specify 'start'/'stop')";
    public string[] Aliases => [];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var entity = ctx.State.LocalPlayer.Entity;
        
        // Check for explicit start/stop
        if (ctx.Arguments.Length > 0)
        {
            var arg = ctx.Arguments[0].ToLowerInvariant();
            switch (arg)
            {
                case "start" or "on":
                    entity.StartJumping();
                    await ctx.SendChatAsync("Jumping.");
                    return;
                case "stop" or "off":
                    entity.StopJumping();
                    await ctx.SendChatAsync("Stopped jumping.");
                    return;
            }
        }

        // Toggle
        if (entity.IsJumping)
        {
            entity.StopJumping();
            await ctx.SendChatAsync("Stopped jumping.");
        }
        else
        {
            entity.StartJumping();
            await ctx.SendChatAsync("Jumping.");
        }
    }
}
