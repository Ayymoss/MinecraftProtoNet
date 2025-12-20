using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("say", Description = "Send a signed chat message")]
public class SayCommand : ICommand
{
    public string Name => "say";
    public string Description => "Send a signed chat message";
    public string[] Aliases => [];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.HasMinArgs(1))
        {
            await ctx.SendUnsignedChatAsync("Usage: !say <message>");
            return;
        }

        var message = ctx.GetRemainingArgsAsString(0);
        await ChatActions.SendSignedMessageAsync(ctx, message);
    }
}
