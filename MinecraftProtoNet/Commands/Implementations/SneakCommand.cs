using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("sneak", Description = "Toggle sneaking")]
public class SneakCommand : ICommand
{
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
                    entity.StartSneaking();
                    await ctx.SendChatAsync("Sneaking.");
                    return;
                case "stop" or "off":
                    entity.StopSneaking();
                    await ctx.SendChatAsync("Stopped sneaking.");
                    return;
            }
        }

        // Toggle
        if (entity.IsSneaking)
        {
            entity.StopSneaking();
            await ctx.SendChatAsync("Stopped sneaking.");
        }
        else
        {
            entity.StartSneaking();
            await ctx.SendChatAsync("Sneaking.");
        }
    }
}
