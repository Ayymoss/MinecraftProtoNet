using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Core.Commands;
using MinecraftProtoNet.Core.Core;

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
            var logger = LoggingConfiguration.CreateLogger<BaritoneCommand>();
            logger.LogDebug($"BaritoneCommand: Created/retrieved Baritone instance. Total instances: {provider.GetAllBaritones().Count}");
            
            // Join remaining arguments into command string
            var commandString = ctx.GetRemainingArgsAsString(0);
            
            // Execute the command via Baritone's command manager
            baritone.GetCommandManager().Execute(commandString);
            
            // Baritone commands typically send their own feedback via LogDirect
            // So we don't need to send a confirmation message here
        }
        catch (Exception ex)
        {
            // Log the full exception with stack trace to the logging infrastructure
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/Helper.java:239-244
            var logger = LoggingConfiguration.CreateLogger<BaritoneCommand>();
            logger.LogError(ex, "Baritone command error: {Message}", ex.Message);
            
            // Also send a user-friendly message to chat
            await ctx.SendChatAsync($"Baritone command error: {ex.Message}");
        }
    }
}

