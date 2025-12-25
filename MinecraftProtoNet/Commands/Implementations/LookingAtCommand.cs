using MinecraftProtoNet.Actions;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("lookingat", Description = "Display block being looked at")]
public class LookingAtCommand : ICommand
{
    public string Name => "lookingat";
    public string Description => "Display information about the block being looked at";
    public string[] Aliases => ["block"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var hit = QueryActions.GetLookedAtBlock(ctx);
        if (hit is null)
        {
            await ctx.SendChatAsync("I'm not looking at a block.");
            return;
        }

        var cursorPos = hit.GetInBlockPosition();
        var placementPos = hit.GetAdjacentBlockPosition();

        var messages = new[]
        {
            $"Name: {hit.Block?.Name} - Pos: {hit.BlockPosition}",
            $"Distance: {hit.Distance:N2}",
            $"Cursor: {cursorPos}",
            $"Face: {hit.Face}",
            $"Placement Pos: {placementPos}"
        };

        foreach (var message in messages)
        {
            await ctx.SendChatAsync(message);
            await Task.Delay(100);
        }
    }
}
