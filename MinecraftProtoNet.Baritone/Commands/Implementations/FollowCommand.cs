using MinecraftProtoNet.Commands;
using MinecraftProtoNet.Services;

namespace MinecraftProtoNet.Baritone.Commands.Implementations;

[Command("follow", Description = "Follow a player or entity")]
public class FollowCommand(IPathingService pathingService) : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.HasMinArgs(1))
        {
            await ctx.SendChatAsync("Usage: follow <playername> | follow cancel | follow stop");
            return;
        }

        var arg = ctx.Arguments[0];

        if (arg.Equals("cancel", StringComparison.OrdinalIgnoreCase) || 
            arg.Equals("stop", StringComparison.OrdinalIgnoreCase))
        {
            if (ctx.State.LocalPlayer.Entity != null)
            {
                pathingService.ForceCancel(ctx.State.LocalPlayer.Entity);
                await ctx.SendChatAsync("Stopped following.");
            }
            return;
        }

        // Try to find player by name
        var targetPlayer = ctx.State.Level.GetPlayerByUsername(arg);

        if (targetPlayer != null)
        {
            if (targetPlayer.Entity != null)
            {
                pathingService.StartFollowing(targetPlayer.Entity);
                await ctx.SendChatAsync($"Following player {targetPlayer.Username}...");
                return;
            }
            
            await ctx.SendChatAsync($"Player '{arg}' found in tab list but no entity visible (too far?).");
            return;
        }

        // Try to find entity by ID
        if (int.TryParse(arg, out var entityId))
        {
            var entity = ctx.State.Level.GetEntityOfId(entityId);
            if (entity != null)
            {
                pathingService.StartFollowing(entity);
                await ctx.SendChatAsync($"Following entity {entityId}...");
                return;
            }
        }
        
        await ctx.SendChatAsync($"Could not find player or entity '{arg}'.");
    }
}
