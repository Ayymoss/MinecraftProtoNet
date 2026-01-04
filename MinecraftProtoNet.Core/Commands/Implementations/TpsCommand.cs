using MinecraftProtoNet.Core.Actions;

namespace MinecraftProtoNet.Core.Commands.Implementations;

[Command("tps", Description = "Display server TPS and tick interval")]
public class TpsCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        var (tps, mspt) = QueryActions.GetServerPerformance(ctx);
        var message = $"TPS: {tps:N2} | MSPT: {mspt:N2}ms";
        await ctx.SendChatAsync(message);
    }
}
