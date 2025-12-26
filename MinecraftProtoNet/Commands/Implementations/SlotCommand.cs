using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("slot", Description = "Change or display held slot")]
public class SlotCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return;

        var entity = ctx.State.LocalPlayer.Entity;

        if (ctx.TryGetArg(0, out short slot))
        {
            if (slot is < 0 or > 8)
            {
                await ctx.SendChatAsync("Slot must be between 0 and 8.");
                return;
            }

            var success = await InteractionActions.SetHeldSlotAsync(ctx, slot);
            if (!success)
            {
                await ctx.SendChatAsync("Failed to change slot.");
            }
        }
        else
        {
            await ctx.SendChatAsync($"Slot Held: {entity.HeldSlot} (0-8)");
        }
    }
}
