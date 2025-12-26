using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("sprint", Description = "Toggle sprinting")]
public class SprintCommand : ICommand
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
                    entity.StartSprinting();
                    entity.Forward = true; // Sprint requires forward movement
                    await ctx.SendChatAsync("Sprinting.");
                    return;
                case "stop" or "off":
                    entity.StopSprinting();
                    await ctx.SendChatAsync("Stopped sprinting.");
                    return;
            }
        }

        // Toggle
        if (entity.WantsToSprint)
        {
            entity.StopSprinting();
            await ctx.SendChatAsync("Stopped sprinting.");
        }
        else
        {
            entity.StartSprinting();
            entity.Forward = true; // Sprint requires forward movement
            await ctx.SendChatAsync("Sprinting.");
        }
    }
}
