namespace MinecraftProtoNet.Core.Commands.Implementations;

[Command("placeit", Description = "Complex block placement routine")]
public class PlaceItCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
