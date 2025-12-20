using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("state", Description = "Display player state")]
public class StateCommand : ICommand
{
    public string Name => "state";
    public string Description => "Display current player state";
    public string[] Aliases => ["status"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var state = QueryActions.GetPlayerState(ctx);
        if (state is null)
        {
            await ctx.SendChatAsync("Player state not available.");
            return;
        }

        var message = $"Pos: {state.Position.X:N2}, {state.Position.Y:N2}, {state.Position.Z:N2}, " +
                      $"Sp: {state.IsSprinting}, J: {state.IsJumping}, Sn: {state.IsSneaking}";
        await ctx.SendChatAsync(message);
    }
}
