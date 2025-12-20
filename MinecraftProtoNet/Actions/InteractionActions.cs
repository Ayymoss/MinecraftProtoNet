using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Actions;

/// <summary>
/// Actions related to entity and block interactions.
/// </summary>
public static class InteractionActions
{
    /// <summary>
    /// Attacks the entity the player is currently looking at.
    /// </summary>
    public static async Task<bool> AttackLookedAtEntityAsync(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return false;

        var entity = ctx.State.LocalPlayer.Entity;
        var entities = ctx.State.Level.GetAllPlayers().Where(x => x.HasEntity).Select(x => x.Entity!);
        var target = entity.GetLookingAtEntity(entity, entities, 3.5);

        if (target is null) return false;

        var yawPitchToTarget = entity.GetYawPitchToTarget(entity, target);
        await ctx.SendPacketAsync(new MovePlayerRotationPacket
        {
            Yaw = yawPitchToTarget.X,
            Pitch = yawPitchToTarget.Y,
            Flags = MovementFlags.None
        });

        await ctx.SendPacketAsync(new InteractPacket
        {
            EntityId = target.EntityId,
            Type = InteractType.Attack,
            SneakKeyPressed = entity.IsSneaking
        });
        await ctx.SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });
        return true;
    }

    /// <summary>
    /// Swings the specified hand.
    /// </summary>
    public static Task SwingHandAsync(IActionContext ctx, Hand hand)
    {
        return ctx.SendPacketAsync(new SwingPacket { Hand = hand });
    }

    /// <summary>
    /// Places a block at the position the player is looking at.
    /// </summary>
    public static async Task<bool> PlaceBlockAsync(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return false;

        var entity = ctx.State.LocalPlayer.Entity;
        if (entity.HeldItem.ItemId is null) return false;

        var hit = entity.GetLookingAtBlock(ctx.State.Level);
        if (hit is null || hit.Distance > 6) return false;

        var cursor = hit.GetInBlockPosition();
        await ctx.SendPacketAsync(new UseItemOnPacket
        {
            Hand = Hand.MainHand,
            Position = new Vector3<double>(hit.BlockPosition.X, hit.BlockPosition.Y, hit.BlockPosition.Z),
            BlockFace = hit.Face,
            Cursor = new Vector3<float>(cursor.X, cursor.Y, cursor.Z),
            InsideBlock = false,
            Sequence = entity.IncrementSequence()
        });
        await ctx.SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });
        return true;
    }

    /// <summary>
    /// Drops the currently held item stack.
    /// </summary>
    public static async Task<bool> DropHeldItemAsync(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return false;

        var entity = ctx.State.LocalPlayer.Entity;
        if (entity.HeldItem.ItemId is null) return false;

        await ctx.SendPacketAsync(new PlayerActionPacket
        {
            Status = PlayerActionPacket.StatusType.DropItemStack,
            Position = new Vector3<double>(0, 0, 0),
            Face = BlockFace.Bottom,
            Sequence = 0
        });
        entity.Inventory[(short)(entity.HeldSlot + 36)] = new Slot();
        return true;
    }

    /// <summary>
    /// Sets the player's held hotbar slot (0-8).
    /// </summary>
    public static async Task<bool> SetHeldSlotAsync(IActionContext ctx, short slot)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return false;
        if (slot is < 0 or > 8) return false;

        await ctx.SendPacketAsync(new SetCarriedItemPacket { Slot = slot });
        ctx.State.LocalPlayer.Entity.HeldSlot = slot;
        return true;
    }

    /// <summary>
    /// Gets the slot number of the currently held item.
    /// </summary>
    public static short? GetHeldSlot(IActionContext ctx)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return null;
        return ctx.State.LocalPlayer.Entity.HeldSlot;
    }
}
