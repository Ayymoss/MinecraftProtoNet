using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("place", Description = "Place a block")]
public class PlaceCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity)
        {
            return;
        }

        if (ctx.State.LocalPlayer.Entity.HeldItem.ItemId is null)
        {
            await ctx.SendChatAsync("You are not holding anything.");
            return;
        }

        var success = await InteractionActions.PlaceBlockAsync(ctx);
        if (!success)
        {
            await ctx.SendChatAsync("Cannot place block here.");
        }
    }
}
