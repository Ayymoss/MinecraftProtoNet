using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Commands;
using MinecraftProtoNet.Services;
using Serilog;

namespace MinecraftProtoNet.Baritone.Commands.Implementations;

[Command("here", Description = "Pathfind to sender's position", PlayerContextRequired = true)]
public class HereCommand(IPathingService pathingService) : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        var entity = ctx.State.LocalPlayer.Entity;
        if (entity == null)
        {
            await ctx.SendChatAsync("Local player entity not found.");
            return;
        }

        // Check for "cancel" or "stop" as first arg
        if (ctx.Arguments.Length > 0 &&
            (ctx.Arguments[0].Equals("cancel", StringComparison.OrdinalIgnoreCase) ||
             ctx.Arguments[0].Equals("stop", StringComparison.OrdinalIgnoreCase)))
        {
            pathingService.ForceCancel(entity);
            await ctx.SendChatAsync("Pathfinding cancelled.");
            return;
        }

        // Check for "status"
        if (ctx.Arguments.Length > 0 &&
            ctx.Arguments[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            if (pathingService.IsPathing)
            {
                await ctx.SendChatAsync($"Pathing to {pathingService.Goal}. Calculating: {pathingService.IsCalculating}");
            }
            else
            {
                await ctx.SendChatAsync("Not currently pathing.");
            }

            return;
        }

        // Get sender
        var sender = ctx.Sender;
        if (sender?.Entity == null)
        {
            await ctx.SendChatAsync("Could not find sender.");
            return;
        }

        // Cancel any existing path
        if (pathingService.IsPathing || pathingService.IsCalculating)
        {
            pathingService.ForceCancel(entity);
        }

        // Get sender's position
        var senderPos = sender.Entity.Position;
        var goalX = (int)Math.Floor(senderPos.X);
        var goalY = (int)Math.Floor(senderPos.Y);
        var goalZ = (int)Math.Floor(senderPos.Z);

        // Create goal (near the sender, within 2 blocks)
        var goal = new GoalNear(goalX, goalY, goalZ, 2);

        // Check if already at goal
        var currentPos = (
            (int)Math.Floor(entity.Position.X),
            (int)Math.Floor(entity.Position.Y),
            (int)Math.Floor(entity.Position.Z)
        );

        if (goal.IsInGoal(currentPos.Item1, currentPos.Item2, currentPos.Item3))
        {
            await ctx.SendChatAsync("Already near you!");
            return;
        }

        // Wire up events
        pathingService.OnPathCalculated += path =>
        {
            Log.Information("[Here] Path calculated: {PathLength} positions, reaches goal: {PathReachesGoal}", path.Length,
                path.ReachesGoal);
        };

        pathingService.OnPathComplete += success =>
        {
            Log.Information("[Here] Path to sender {CompletedSuccessfully}", success ? "completed successfully" : "failed");
        };

        // Start pathfinding
        var started = pathingService.SetGoalAndPath(goal, entity);

        if (started)
        {
            var dist = Math.Sqrt(
                Math.Pow(goalX - entity.Position.X, 2) +
                Math.Pow(goalY - entity.Position.Y, 2) +
                Math.Pow(goalZ - entity.Position.Z, 2));
            await ctx.SendChatAsync($"Coming to you ({dist:F0} blocks away)...");
        }
        else
        {
            await ctx.SendChatAsync("Failed to start pathfinding. Already pathing or at goal.");
        }
    }
}
