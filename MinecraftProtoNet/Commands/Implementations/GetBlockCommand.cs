using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("getblock", Description = "Get block at coordinates")]
public class GetBlockCommand : ICommand
{
    public string Name => "getblock";
    public string Description => "Get block at coordinates (x y z)";
    public string[] Aliases => [];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.TryGetArg(0, out int x) ||
            !ctx.TryGetArg(1, out int y) ||
            !ctx.TryGetArg(2, out int z))
        {
            await ctx.SendUnsignedChatAsync("Usage: !getblock <x> <y> <z>");
            return;
        }

        var block = QueryActions.GetBlockAt(ctx, x, y, z);
        var message = block != null
            ? $"Block: ({block.Id}) {block.Name}"
            : $"Block not found at {x}, {y}, {z}";

        Console.WriteLine(message);
    }
}
