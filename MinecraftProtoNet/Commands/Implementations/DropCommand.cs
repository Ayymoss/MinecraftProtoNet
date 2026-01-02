using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("drop", Description = "Drop held item stack")]
public class DropCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return;

        if (ctx.State.LocalPlayer.Entity.HeldItem.ItemId is null)
        {
            await ctx.SendChatAsync("You are not holding anything.");
            return;
        }

        await ctx.Client.InteractionManager.DropHeldItemAsync();
    }
}
