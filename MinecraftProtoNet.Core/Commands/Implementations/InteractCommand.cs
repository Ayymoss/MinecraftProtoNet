using MinecraftProtoNet.Core.Enums;

namespace MinecraftProtoNet.Core.Commands.Implementations;

/// <summary>
/// Command to right-click (interact with) the entity being looked at.
/// Opens container UIs for villagers, NPCs with custom menus, etc.
/// </summary>
[Command("interact", Aliases = ["use"], Description = "Right-click the entity being looked at")]
public class InteractCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        // Parse optional hand argument
        var hand = Hand.MainHand;
        if (ctx.Arguments.Length > 0 && ctx.Arguments[0].Equals("offhand", StringComparison.OrdinalIgnoreCase))
        {
            hand = Hand.OffHand;
        }

        var success = await ctx.Client.InteractionManager.InteractAsync(hand);
        if (!success)
        {
            await ctx.SendChatAsync("I'm not looking at an entity.");
        }
    }
}
