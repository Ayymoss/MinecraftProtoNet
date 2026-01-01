using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.State;

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
    /// Right-clicks (interacts with) the entity or block the player is currently looking at.
    /// This opens container UIs for villagers, NPCs, chests, doors, etc.
    /// Follows Minecraft's right-click behavior: try entity first, then block.
    /// </summary>
    public static async Task<bool> InteractWithLookedAtEntityAsync(IActionContext ctx, Hand hand = Hand.MainHand)
    {
        if (!ctx.State.LocalPlayer.HasEntity) return false;

        var localEntity = ctx.State.LocalPlayer.Entity;
        var eyePos = localEntity.EyePosition;
        var lookDir = localEntity.GetLookDirection();
        const double maxDistance = 4.5;

        // First, try to find an entity in the look direction
        var entityTarget = FindEntityInLookDirection(ctx, eyePos, lookDir, maxDistance);

        if (entityTarget != null)
        {
            Console.WriteLine($"[Interact] Selected entity {entityTarget.EntityId} type={entityTarget.EntityType}");

            // Look at the target
            var targetCenter = entityTarget.Position + new Models.Core.Vector3<double>(0, 1.0, 0);
            var toTarget = targetCenter - eyePos;
            var yaw = (float)(Math.Atan2(-toTarget.X, toTarget.Z) * (180.0 / Math.PI));
            var horizontalDist = Math.Sqrt(toTarget.X * toTarget.X + toTarget.Z * toTarget.Z);
            var pitch = (float)(Math.Atan2(-toTarget.Y, horizontalDist) * (180.0 / Math.PI));

            await ctx.SendPacketAsync(new MovePlayerRotationPacket
            {
                Yaw = yaw,
                Pitch = pitch,
                Flags = MovementFlags.None
            });

            await ctx.SendPacketAsync(new InteractPacket
            {
                EntityId = entityTarget.EntityId,
                Type = InteractType.Interact,
                Hand = hand,
                SneakKeyPressed = localEntity.IsSneaking
            });
            await ctx.SendPacketAsync(new SwingPacket { Hand = hand });
            return true;
        }

        // No entity found - fall back to block interaction (like Minecraft's right-click)
        Console.WriteLine("[Interact] No entity found, checking for block...");

        var blockHit = localEntity.GetLookingAtBlock(ctx.State.Level);
        if (blockHit == null || blockHit.Distance > 5.0)
        {
            Console.WriteLine("[Interact] No block found in range");
            return false;
        }

        var blockId = ctx.State.Level.GetBlockAt(
            blockHit.BlockPosition.X,
            blockHit.BlockPosition.Y,
            blockHit.BlockPosition.Z);
        Console.WriteLine($"[Interact] Found block at {blockHit.BlockPosition} (id={blockId}), face={blockHit.Face}");

        // Send UseItemOn packet to right-click the block
        var cursor = blockHit.GetInBlockPosition();
        await ctx.SendPacketAsync(new UseItemOnPacket
        {
            Hand = hand,
            Position = new Vector3<double>(blockHit.BlockPosition.X, blockHit.BlockPosition.Y, blockHit.BlockPosition.Z),
            BlockFace = blockHit.Face,
            Cursor = new Vector3<float>(cursor.X, cursor.Y, cursor.Z),
            InsideBlock = false,
            Sequence = localEntity.IncrementSequence()
        });
        await ctx.SendPacketAsync(new SwingPacket { Hand = hand });
        Console.WriteLine($"[Interact] Sent UseItemOn for block at {blockHit.BlockPosition}");
        return true;
    }

    /// <summary>
    /// Searches for an entity the player is looking at.
    /// </summary>
    private static WorldEntity? FindEntityInLookDirection(IActionContext ctx, Vector3<double> eyePos, Vector3<double> lookDir,
        double maxDistance)
    {
        var allEntities = ctx.State.WorldEntities.GetAllEntities();
        Console.WriteLine($"[Interact] Searching {allEntities.Count} entities. Eye: {eyePos}, Look: {lookDir}");

        WorldEntity? bestTarget = null;
        var bestDist = maxDistance * maxDistance;

        foreach (var worldEntity in allEntities)
        {
            var toEntity = worldEntity.Position - eyePos;
            var distSq = toEntity.LengthSquared();

            if (distSq > maxDistance * maxDistance)
            {
                Console.WriteLine(
                    $"[Interact] Entity {worldEntity.EntityId} at {worldEntity.Position} - TOO FAR ({Math.Sqrt(distSq):F2}m)");
                continue;
            }

            // Check if looking roughly at the entity (dot product check)
            var toEntityNorm = toEntity.Normalized();
            var dot = lookDir.Dot(toEntityNorm);
            if (dot < 0.6) // ~53 degree cone
            {
                Console.WriteLine($"[Interact] Entity {worldEntity.EntityId} at {worldEntity.Position} - NOT LOOKING (dot={dot:F3})");
                continue;
            }

            // Simple AABB check - entity is roughly 0.6 wide, ~1.8-2.0 tall
            var entityBox = new AABB(
                worldEntity.Position.X - 0.3,
                worldEntity.Position.Y,
                worldEntity.Position.Z - 0.3,
                worldEntity.Position.X + 0.3,
                worldEntity.Position.Y + 1.9,
                worldEntity.Position.Z + 0.3
            );

            // Ray-AABB intersection check
            if (!RayIntersectsAABB(eyePos, lookDir, entityBox, out var hitDist))
            {
                Console.WriteLine($"[Interact] Entity {worldEntity.EntityId} at {worldEntity.Position} - NO RAY HIT");
                continue;
            }

            if (hitDist * hitDist >= bestDist) continue;

            bestDist = hitDist * hitDist;
            bestTarget = worldEntity;
            Console.WriteLine($"[Interact] Entity {worldEntity.EntityId} at {worldEntity.Position} - CANDIDATE (dist={hitDist:F2})");
        }

        return bestTarget;
    }

    private static bool RayIntersectsAABB(Models.Core.Vector3<double> origin, Models.Core.Vector3<double> dir, AABB box,
        out double distance)
    {
        distance = 0;
        var tMin = double.NegativeInfinity;
        var tMax = double.PositiveInfinity;

        // X slab
        if (Math.Abs(dir.X) < 1e-9)
        {
            if (origin.X < box.Min.X || origin.X > box.Max.X) return false;
        }
        else
        {
            var t1 = (box.Min.X - origin.X) / dir.X;
            var t2 = (box.Max.X - origin.X) / dir.X;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        // Y slab  
        if (Math.Abs(dir.Y) < 1e-9)
        {
            if (origin.Y < box.Min.Y || origin.Y > box.Max.Y) return false;
        }
        else
        {
            var t1 = (box.Min.Y - origin.Y) / dir.Y;
            var t2 = (box.Max.Y - origin.Y) / dir.Y;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        // Z slab
        if (Math.Abs(dir.Z) < 1e-9)
        {
            if (origin.Z < box.Min.Z || origin.Z > box.Max.Z) return false;
        }
        else
        {
            var t1 = (box.Min.Z - origin.Z) / dir.Z;
            var t2 = (box.Max.Z - origin.Z) / dir.Z;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        distance = tMin > 0 ? tMin : tMax;
        return distance > 0;
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
        entity.Inventory.SetSlot((short)(entity.HeldSlot + 36), new Slot());
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
