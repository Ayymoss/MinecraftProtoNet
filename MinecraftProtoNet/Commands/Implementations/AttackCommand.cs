using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("attack", Description = "Attack the entity being looked at")]
public class AttackCommand : ICommand
{
    public string Name => "attack";
    public string Description => "Attack the entity being looked at";
    public string[] Aliases => ["hit"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var success = await InteractionActions.AttackLookedAtEntityAsync(ctx);
        if (!success)
        {
            await ctx.SendChatAsync("I'm not looking at a player.");
        }
    }
}
