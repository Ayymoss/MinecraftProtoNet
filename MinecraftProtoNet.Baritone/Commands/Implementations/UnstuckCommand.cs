using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Commands;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Baritone.Commands.Implementations;

/// <summary>
/// Command to help the bot get "unstuck" by repositioning to the center of a nearby safe block.
/// This helps avoid edge cases where the bot is on a block boundary that causes pathfinding issues.
/// </summary>
[Command("unstuck", Description = "Reposition to nearest safe block center")]
public class UnstuckCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        var entity = ctx.State.LocalPlayer.Entity;
        var level = ctx.State.Level;
        
        if (entity == null)
        {
            await ctx.SendChatAsync("Entity not available.");
            return;
        }

        // Current position
        var currentX = entity.Position.X;
        var currentY = entity.Position.Y;
        var currentZ = entity.Position.Z;
        
        var currentBlockX = (int)Math.Floor(currentX);
        var currentBlockY = (int)Math.Floor(currentY);
        var currentBlockZ = (int)Math.Floor(currentZ);

        // Find the best safe position within a small radius
        var bestPos = FindBestSafePosition(level, currentBlockX, currentBlockY, currentBlockZ);
        
        if (bestPos == null)
        {
            await ctx.SendChatAsync("Could not find a safe position nearby. Try jumping or looking for open space.");
            return;
        }

        // Calculate center of the target block
        var targetX = bestPos.Value.X + 0.5;
        var targetY = bestPos.Value.Y;
        var targetZ = bestPos.Value.Z + 0.5;

        // Calculate how far we are from the center
        var distFromCenter = Math.Sqrt(
            Math.Pow(currentX - targetX, 2) + 
            Math.Pow(currentZ - targetZ, 2));

        if (distFromCenter < 0.2)
        {
            await ctx.SendChatAsync($"Already at a safe position. Current: ({currentX:F2}, {currentY:F2}, {currentZ:F2})");
            return;
        }

        // Move to the safe position center
        // We'll set up a simple timed movement towards the center
        var startTime = DateTime.UtcNow;
        var maxDuration = TimeSpan.FromSeconds(3); // Max 3 seconds to center
        
        await ctx.SendChatAsync($"Repositioning to block center ({bestPos.Value.X}, {bestPos.Value.Y}, {bestPos.Value.Z})...");

        while (DateTime.UtcNow - startTime < maxDuration)
        {
            // Update current position
            var posX = entity.Position.X;
            var posZ = entity.Position.Z;
            
            var dist = Math.Sqrt(Math.Pow(posX - targetX, 2) + Math.Pow(posZ - targetZ, 2));
            
            // Strict centering - require true center (0.05 threshold)
            if (dist < 0.05)
            {
                // At true center
                entity.Forward = false;
                entity.Backward = false;
                entity.Left = false;
                entity.Right = false;
                entity.StopSneaking();
                await ctx.SendChatAsync($"Repositioned successfully. Now at ({entity.Position.X:F2}, {entity.Position.Y:F2}, {entity.Position.Z:F2})");
                return;
            }

            // Calculate yaw to face target
            var yaw = MovementHelper.CalculateYaw(posX, posZ, targetX, targetZ);
            entity.YawPitch = new Models.Core.Vector2<float>(yaw, entity.YawPitch.Y);
            
            // Use sneaking for precise movement
            entity.Forward = true;
            entity.StopSprinting();
            entity.StartSneaking();
            
            await Task.Delay(50); // ~20 TPS
        }

        // Timed out
        entity.Forward = false;
        entity.StopSneaking();
        await ctx.SendChatAsync($"Reposition timed out. Current position: ({entity.Position.X:F2}, {entity.Position.Y:F2}, {entity.Position.Z:F2})");
    }

    /// <summary>
    /// Finds the best safe position to stand on within a small radius.
    /// Prioritizes the current block if safe, then checks adjacent blocks.
    /// </summary>
    private static (int X, int Y, int Z)? FindBestSafePosition(Level level, int centerX, int centerY, int centerZ)
    {
        // First check if current block is safe
        if (IsSafeStandingPosition(level, centerX, centerY, centerZ))
        {
            return (centerX, centerY, centerZ);
        }

        // Check adjacent blocks in a spiral pattern (closest first)
        // Order: cardinal directions, then diagonals, then Y levels
        var offsets = new[]
        {
            // Same Y level - cardinal
            (0, 0, -1), (0, 0, 1), (-1, 0, 0), (1, 0, 0),
            // Same Y level - diagonal
            (-1, 0, -1), (1, 0, -1), (-1, 0, 1), (1, 0, 1),
            // One block down - cardinal
            (0, -1, -1), (0, -1, 1), (-1, -1, 0), (1, -1, 0), (0, -1, 0),
            // One block down - diagonal
            (-1, -1, -1), (1, -1, -1), (-1, -1, 1), (1, -1, 1),
            // One block up - cardinal (if we fell into a hole)
            (0, 1, -1), (0, 1, 1), (-1, 1, 0), (1, 1, 0), (0, 1, 0),
        };

        foreach (var (dx, dy, dz) in offsets)
        {
            var testX = centerX + dx;
            var testY = centerY + dy;
            var testZ = centerZ + dz;

            if (IsSafeStandingPosition(level, testX, testY, testZ))
            {
                return (testX, testY, testZ);
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a position is safe to stand on:
    /// - Floor block that can be walked on
    /// - Body and head space are passable
    /// </summary>
    private static bool IsSafeStandingPosition(Level level, int x, int y, int z)
    {
        var floor = level.GetBlockAt(x, y - 1, z);
        var body = level.GetBlockAt(x, y, z);
        var head = level.GetBlockAt(x, y + 1, z);

        // Need solid floor, passable body and head
        return MovementHelper.CanWalkOn(floor) &&
               MovementHelper.CanWalkThrough(body) &&
               MovementHelper.CanWalkThrough(head);
    }
}
