using MinecraftProtoNet.Core.Actions;

namespace MinecraftProtoNet.Core.Commands.Implementations;

[Command("getblock", Description = "Get block at coordinates")]
public class GetBlockCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.TryGetArg(0, out int x) ||
            !ctx.TryGetArg(1, out int y) ||
            !ctx.TryGetArg(2, out int z))
        {
            await ctx.SendChatAsync("Usage: !getblock <x> <y> <z>");
            return;
        }

        var block = QueryActions.GetBlockAt(ctx, x, y, z);
        var message = block != null
            ? $"Block: ({block.Id}) {block.Name}"
            : $"Block not found at {x}, {y}, {z}";

        await ctx.SendChatAsync(message);
    }
}
