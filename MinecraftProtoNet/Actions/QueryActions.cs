using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.State.Base;

namespace MinecraftProtoNet.Actions;

/// <summary>
/// Query actions for retrieving game state information.
/// </summary>
public static class QueryActions
{
    /// <summary>
    /// Gets the block at the specified coordinates.
    /// </summary>
    public static BlockState? GetBlockAt(IActionContext ctx, int x, int y, int z)
    {
        return ctx.State.Level.GetBlockAt(x, y, z);
    }

    /// <summary>
    /// Gets information about the block the player is looking at.
    /// </summary>
    public static RaycastHit? GetLookedAtBlock(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return null;
        return ctx.State.LocalPlayer.Entity.GetLookingAtBlock(ctx.State.Level);
    }

    /// <summary>
    /// Gets the currently held item.
    /// </summary>
    public static Slot? GetHeldItem(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return null;
        return ctx.State.LocalPlayer.Entity.HeldItem;
    }

    /// <summary>
    /// Gets the server's current TPS and tick interval.
    /// </summary>
    public static (double Tps, double Mspt) GetServerPerformance(IActionContext ctx)
    {
        return (ctx.State.Level.GetCurrentServerTps(),ctx.State.Level.TickInterval);
    }

    /// <summary>
    /// Gets the current player state.
    /// </summary>
    public static PlayerStateInfo? GetPlayerState(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return null;
        var entity = ctx.State.LocalPlayer.Entity;
        return new PlayerStateInfo(
            entity.Position,
            entity.IsSprinting || entity.WantsToSprint,
            entity.IsJumping,
            entity.IsSneaking
        );
    }
}

/// <summary>
/// Snapshot of player state information.
/// </summary>
public record PlayerStateInfo(
    Vector3<double> Position,
    bool IsSprinting,
    bool IsJumping,
    bool IsSneaking
);
