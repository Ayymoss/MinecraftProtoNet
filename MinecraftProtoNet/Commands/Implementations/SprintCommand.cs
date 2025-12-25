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
        var isSprinting = await MovementActions.ToggleSprintingAsync(ctx);
        var message = isSprinting ? "Sprinting!" : "Stopped sprinting.";
        await ctx.SendChatAsync(message);
    }
}
