using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("slot", Description = "Change or display held slot")]
public class SlotCommand : ICommand
{
    public string Name => "slot";
    public string Description => "Change held slot (0-8) or display current slot";
    public string[] Aliases => ["hotbar"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return;

        var entity = ctx.State.LocalPlayer.Entity;

        if (ctx.TryGetArg(0, out short slot))
        {
            if (slot is < 0 or > 8)
            {
                await ctx.SendUnsignedChatAsync("Slot must be between 0 and 8.");
                return;
            }

            var success = await InteractionActions.SetHeldSlotAsync(ctx, slot);
            if (!success)
            {
                await ctx.SendUnsignedChatAsync("Failed to change slot.");
            }
        }
        else
        {
            await ctx.SendUnsignedChatAsync($"Slot Held: {entity.HeldSlot} (0-8)");
        }
    }
}
