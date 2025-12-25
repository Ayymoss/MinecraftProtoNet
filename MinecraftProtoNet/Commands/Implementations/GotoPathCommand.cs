using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Pathfinding.Goals;
using MinecraftProtoNet.Services;
using Serilog;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("gotopath", Description = "Pathfind to coordinates")]
public class GotoPathCommand : ICommand
{
    public string Name => "gotopath";
    public string Description => "Pathfind to coordinates (x y z)";
    public string[] Aliases => ["path", "goto"];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        // Check for "cancel" or "stop" as first arg
        if (ctx.Arguments.Length > 0 && 
            (ctx.Arguments[0].Equals("cancel", StringComparison.OrdinalIgnoreCase) ||
             ctx.Arguments[0].Equals("stop", StringComparison.OrdinalIgnoreCase)))
        {
            ctx.Client.PathingService.ForceCancel(ctx.State.LocalPlayer.Entity);
            await ctx.SendChatAsync("Pathfinding cancelled.");
            return;
        }

        // Check for "status"
        if (ctx.Arguments.Length > 0 && 
            ctx.Arguments[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            var pathing = ctx.Client.PathingService;
            if (pathing.IsPathing)
            {
                await ctx.SendChatAsync($"Pathing to {pathing.Goal}. Calculating: {pathing.IsCalculating}");
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

        var pathingService = ctx.Client.PathingService;
        var entity = ctx.State.LocalPlayer.Entity;

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
