using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("forward", Description = "Toggle forward movement")]
public class ForwardCommand : ICommand
{
    public string Name => "forward";
    public string Description => "Toggle forward movement (on/off or specify 'start'/'stop')";
    public string[] Aliases => ["walk"];

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
                    entity.Forward = true;
                    await ctx.SendChatAsync("Moving forward.");
                    return;
                case "stop" or "off":
                    entity.Forward = false;
                    await ctx.SendChatAsync("Stopped moving forward.");
                    return;
            }
        }

        // Toggle
        entity.Forward = !entity.Forward;
        await ctx.SendChatAsync(entity.Forward ? "Moving forward." : "Stopped moving forward.");
    }
}
