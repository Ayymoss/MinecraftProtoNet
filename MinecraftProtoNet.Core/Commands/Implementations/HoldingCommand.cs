using MinecraftProtoNet.Core.Actions;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Commands.Implementations;

[Command("holding", Description = "Display held item")]
public class HoldingCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        var heldItem = QueryActions.GetHeldItem(ctx);
        if (heldItem?.ItemId is null)
        {
            await ctx.SendChatAsync("You are not holding anything.");
            return;
        }

        var itemName = ClientState.ItemRegistry[heldItem.ItemId.Value];
        var message = $"Holding: {heldItem.ItemCount}x of {itemName} ({heldItem.ItemId})";
        await ctx.SendChatAsync(message);
    }
}
