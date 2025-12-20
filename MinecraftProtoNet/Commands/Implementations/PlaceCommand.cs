using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("place", Description = "Place a block")]
public class PlaceCommand : ICommand
{
    public string Name => "place";
    public string Description => "Place a block at the looked-at position";
    public string[] Aliases => [];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity)
        {
            return;
        }

        if (ctx.State.LocalPlayer.Entity.HeldItem.ItemId is null)
        {
            await ctx.SendUnsignedChatAsync("You are not holding anything.");
            return;
        }

        var success = await InteractionActions.PlaceBlockAsync(ctx);
        if (!success)
        {
            await ctx.SendUnsignedChatAsync("Cannot place block here.");
        }
    }
}
