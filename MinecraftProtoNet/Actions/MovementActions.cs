using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Actions;

/// <summary>
/// Actions related to player movement and positioning.
/// </summary>
public static class MovementActions
{
    /// <summary>
    /// Interpolates the player to the target coordinates at the specified speed.
    /// </summary>
    public static Task MoveToAsync(IActionContext ctx, Vector3<double> target, float speed = 0.25f)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return Task.CompletedTask;
        ClientManagerHelpers.InterpolateToCoordinates(ctx.Client, target, speed);
        // TODO: Implement interpolation logic directly or via a new service
        return Task.CompletedTask;
    }

    /// <summary>
    /// Uses pathfinding to navigate to the target coordinates.
    /// Uses the client's shared PathFollowerService to ensure the physics loop will process the path.
    /// </summary>
    public static bool PathfindTo(IActionContext ctx, Vector3<double> target)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return false;
        var pathService = ctx.Client.PathFollowerService;
        pathService.Initialize(ctx.State.Level);
        return pathService.FollowPathTo(ctx.State.LocalPlayer.Entity, target);
    }


    /// <summary>
    /// Rotates the player to look at specific coordinates.
    /// </summary>
    public static async Task LookAtAsync(IActionContext ctx, float x, float y, float z, BlockFace? face = null)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return;

        var targetX = x + 0.5f;
        var targetY = y + 0.5f;
        var targetZ = z + 0.5f;

        if (face.HasValue)
        {
            switch (face)
            {
                case BlockFace.Bottom:
                    targetY = y;
                    break;
                case BlockFace.Top:
                    targetY = y + 1.0f;
                    break;
                case BlockFace.North:
                    targetZ = z;
                    break;
                case BlockFace.South:
                    targetZ = z + 1.0f;
                    break;
                case BlockFace.West:
                    targetX = x;
                    break;
                case BlockFace.East:
                    targetX = x + 1.0f;
                    break;
            }
        }

        var entity = ctx.State.LocalPlayer.Entity;
        var playerEyeX = entity.Position.X;
        var playerEyeY = entity.Position.Y + 1.62f;
        var playerEyeZ = entity.Position.Z;

        var dx = targetX - playerEyeX;
        var dy = targetY - playerEyeY;
        var dz = targetZ - playerEyeZ;

        var yaw = (float)(-Math.Atan2(dx, dz) * (180 / Math.PI));
        var horizontalDistance = Math.Sqrt(dx * dx + dz * dz);
        var pitch = (float)(-Math.Atan2(dy, horizontalDistance) * (180 / Math.PI));

        await ctx.SendPacketAsync(new MovePlayerRotationPacket
        {
            Yaw = yaw,
            Pitch = pitch,
            Flags = MovementFlags.None
        });

        entity.YawPitch = new Vector2<float>(yaw, pitch);
    }

    /// <summary>
    /// Toggles the player's forward movement state.
    /// </summary>
    public static bool ToggleForward(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return false;
        var entity = ctx.State.LocalPlayer.Entity;
        entity.Forward = !entity.Forward;
        return entity.Forward;
    }

    /// <summary>
    /// Toggles the player's jumping state.
    /// </summary>
    public static bool ToggleJumping(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return false;
        var entity = ctx.State.LocalPlayer.Entity;
        if (entity.IsJumping)
        {
            entity.StopJumping();
            return false;
        }
        entity.StartJumping();
        return true;
    }

    /// <summary>
    /// Toggles the player's sneaking state.
    /// </summary>
    public static async Task<bool> ToggleSneakingAsync(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return false;
        var entity = ctx.State.LocalPlayer.Entity;

        if (entity.IsSneaking)
        {
            entity.StopSneaking();
            await ctx.SendPacketAsync(new PlayerCommandPacket
            {
                EntityId = entity.EntityId,
                Action = PlayerAction.StopSneaking
            });
            return false;
        }

        // Stop sprinting first if active
        if (entity.WantsToSprint)
        {
            entity.StopSprinting();
            await ctx.SendPacketAsync(new PlayerCommandPacket
            {
                EntityId = entity.EntityId,
                Action = PlayerAction.StopSprint
            });
        }

        entity.StartSneaking();
        await ctx.SendPacketAsync(new PlayerCommandPacket
        {
            EntityId = entity.EntityId,
            Action = PlayerAction.StartSneaking
        });
        return true;
    }

    /// <summary>
    /// Toggles the player's sprinting state.
    /// </summary>
    public static async Task<bool> ToggleSprintingAsync(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return false;
        var entity = ctx.State.LocalPlayer.Entity;

        if (entity.WantsToSprint)
        {
            await ctx.SendPacketAsync(new PlayerCommandPacket
            {
                EntityId = entity.EntityId,
                Action = PlayerAction.StopSprint,
            });
            entity.StopSprinting();
            return false;
        }

        // Stop sneaking first if active
        if (entity.IsSneaking)
        {
            entity.StopSneaking();
            await ctx.SendPacketAsync(new PlayerCommandPacket
            {
                EntityId = entity.EntityId,
                Action = PlayerAction.StopSneaking
            });
        }

        entity.StartSprinting();
        return true;
    }
}
