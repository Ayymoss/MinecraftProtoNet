using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Enums;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("lookat", Description = "Look at coordinates")]
public class LookAtCommand : ICommand
{
    public string Name => "lookat";
    public string Description => "Look at coordinates (x y z [face])";
    public string[] Aliases => ["look"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.TryGetArg(0, out float x) ||
            !ctx.TryGetArg(1, out float y) ||
            !ctx.TryGetArg(2, out float z))
        {
            await ctx.SendUnsignedChatAsync("Usage: !lookat <x> <y> <z> [face]");
            return;
        }

        BlockFace? face = BlockFace.Top;
        if (ctx.TryGetArg(3, out string faceStr) && Enum.TryParse<BlockFace>(faceStr, true, out var parsedFace))
        {
            face = parsedFace;
        }

        await MovementActions.LookAtAsync(ctx, x, y, z, face);
    }
}
