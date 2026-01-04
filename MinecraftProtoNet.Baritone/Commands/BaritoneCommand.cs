using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Core.Commands;

namespace MinecraftProtoNet.Baritone.Commands;

/// <summary>
/// Command to proxy Baritone commands.
/// Usage: !baritone <command> or !bt <command>
/// Example: !bt goto 100 64 200
/// </summary>
[Command("baritone", Aliases = ["bt"], Description = "Execute Baritone commands")]
public class BaritoneCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.HasMinArgs(1))
        {
            await ctx.SendChatAsync("Usage: !baritone <command> or !bt <command>");
            await ctx.SendChatAsync("Example: !bt goto 100 64 200");
            return;
        }

        try
        {
            // Get Baritone provider
            var provider = BaritoneAPI.GetProvider();
            
            // Get or create Baritone instance for this client
            var baritone = provider.CreateBaritone(ctx.Client);
            
            // Join remaining arguments into command string
            var commandString = ctx.GetRemainingArgsAsString(0);
            
            // Execute the command via Baritone's command manager
            baritone.GetCommandManager().Execute(commandString);
            
            // Baritone commands typically send their own feedback via LogDirect
            // So we don't need to send a confirmation message here
        }
        catch (Exception ex)
        {
            await ctx.SendChatAsync($"Baritone command error: {ex.Message}");
        }
    }
}

