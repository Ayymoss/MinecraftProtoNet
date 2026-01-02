using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Commands;
using MinecraftProtoNet.Services;
using Serilog;

namespace MinecraftProtoNet.Baritone.Commands.Implementations;

[Command("gotopath", Description = "Pathfind to coordinates")]
public class GotoPathCommand(IPathingService pathingService) : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        // Check for "cancel" or "stop" as first arg
        if (ctx.Arguments.Length > 0 && 
            (ctx.Arguments[0].Equals("cancel", StringComparison.OrdinalIgnoreCase) ||
             ctx.Arguments[0].Equals("stop", StringComparison.OrdinalIgnoreCase)))
        {
            if (ctx.State.LocalPlayer.Entity != null)
            {
                pathingService.ForceCancel(ctx.State.LocalPlayer.Entity);
            }
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


        // Parse coordinates
        if (!ctx.HasMinArgs(3))
        {
            await ctx.SendChatAsync("Usage: gotopath <x> <y> <z> | gotopath cancel | gotopath status");
            return;
        }

        if (!ctx.TryGetArg<int>(0, out var x) ||
            !ctx.TryGetArg<int>(1, out var y) ||
            !ctx.TryGetArg<int>(2, out var z))
        {
            await ctx.SendChatAsync("Invalid coordinates. Usage: gotopath <x> <y> <z>");
            return;
        }

        var entity = ctx.State.LocalPlayer.Entity;
        if (entity == null)
        {
            await ctx.SendChatAsync("Local player entity not found.");
            return;
        }

        // Cancel any existing path
        if (pathingService.IsPathing || pathingService.IsCalculating)
        {
            pathingService.ForceCancel(entity);
        }

        // Create goal
        var goal = new GoalBlock(x, y, z);

        // Check if already at goal
        var currentPos = (
            (int)Math.Floor(entity.Position.X),
            (int)Math.Floor(entity.Position.Y),
            (int)Math.Floor(entity.Position.Z)
        );
        
        if (goal.IsInGoal(currentPos.Item1, currentPos.Item2, currentPos.Item3))
        {
            await ctx.SendChatAsync($"Already at destination ({x}, {y}, {z})!");
            return;
        }

        // Wire up events (only once ideally, but for simplicity we do it each time)
        pathingService.OnPathCalculated += path =>
        {
            Log.Information("[Pathfinding] Path calculated: {Length} positions, reaches goal: {ReachesGoal}", path.Length, path.ReachesGoal);
        };
        
        pathingService.OnPathComplete += success =>
        {
            Log.Information("[Pathfinding] Path {Status}", success ? "completed successfully" : "failed");
        };

        // Start pathfinding
        var started = pathingService.SetGoalAndPath(goal, entity);
        
        if (started)
        {
            var dist = Math.Sqrt(
                Math.Pow(x - entity.Position.X, 2) +
                Math.Pow(y - entity.Position.Y, 2) +
                Math.Pow(z - entity.Position.Z, 2));
            await ctx.SendChatAsync($"Pathfinding to ({x}, {y}, {z}) - {dist:F0} blocks away...");
        }
        else
        {
            await ctx.SendChatAsync("Failed to start pathfinding. Already pathing or at goal.");
        }
    }
}
