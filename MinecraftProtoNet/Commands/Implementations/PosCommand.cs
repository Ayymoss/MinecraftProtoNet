namespace MinecraftProtoNet.Commands.Implementations;

[Command("pos", Description = "Display sender's position")]
public class PosCommand : ICommand
{
    public string Name => "pos";
    public string Description => "Display the sender's position";
    public string[] Aliases => ["position"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (ctx.Sender?.HasEntity != true)
        {
            await ctx.SendChatAsync("Sender position not available.");
            return;
        }

        var entity = ctx.Sender.Entity;
        var playerPos = $"{ctx.Sender.Username} -> {entity?.Position.X:N2}, {entity?.Position.Y:N2}, {entity?.Position.Z:N2}";
        await ctx.SendChatAsync($"Last position: {playerPos}");
    }
}
