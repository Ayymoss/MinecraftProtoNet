using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("say", Description = "Send a signed chat message")]
public class SayCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.HasMinArgs(1))
        {
            await ctx.SendChatAsync("Usage: !say <message>");
            return;
        }

        var message = ctx.GetRemainingArgsAsString(0);
        await ctx.SendChatAsync(message);
    }
}
