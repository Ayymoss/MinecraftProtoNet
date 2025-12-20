using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("tps", Description = "Display server TPS and tick interval")]
public class TpsCommand : ICommand
{
    public string Name => "tps";
    public string Description => "Display server TPS and tick interval";
    public string[] Aliases => [];

    public Task ExecuteAsync(CommandContext ctx)
    {
        var (tps, mspt) = QueryActions.GetServerPerformance(ctx);
        Console.WriteLine($"TPS: {tps:N2} | MSPT: {mspt:N2}ms");
        return Task.CompletedTask;
    }
}
