using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("drop", Description = "Drop held item stack")]
public class DropCommand : ICommand
{
    public string Name => "drop";
    public string Description => "Drop the held item stack";
    public string[] Aliases => [];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return;

        if (ctx.State.LocalPlayer.Entity.HeldItem.ItemId is null)
        {
            await ctx.SendUnsignedChatAsync("You are not holding anything.");
            return;
        }

        await InteractionActions.DropHeldItemAsync(ctx);
    }
}
