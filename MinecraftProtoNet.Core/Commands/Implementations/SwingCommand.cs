using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Enums;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("swing", Description = "Swing hand")]
public class SwingCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        var hand = Hand.MainHand;
        if (ctx.TryGetArg(0, out string handStr) && Enum.TryParse<Hand>(handStr, true, out var parsedHand))
        {
            hand = parsedHand;
        }

        await ctx.Client.InteractionManager.SwingHandAsync(hand);
    }
}
