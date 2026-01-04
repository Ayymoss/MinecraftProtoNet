using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("attack", Description = "Attack the entity being looked at")]
public class AttackCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        var success = await ctx.Client.InteractionManager.AttackAsync();
        if (!success)
        {
            await ctx.SendChatAsync("I'm not looking at a player.");
        }
    }
}
