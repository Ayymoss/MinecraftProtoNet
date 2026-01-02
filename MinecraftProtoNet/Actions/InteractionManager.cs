using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Actions;

public class InteractionManager : IInteractionManager
{
    private readonly IMinecraftClient _client;
    private readonly ILogger<InteractionManager> _logger;

    public double ReachDistance { get; set; } = 4.5;

    public InteractionManager(IMinecraftClient client, ILogger<InteractionManager> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> DigBlockAsync()
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        var hit = entity.GetLookingAtBlock(_client.State.Level, ReachDistance);
        if (hit is null) return false;

        _logger.LogInformation("Digging block at {Pos} (Face={Face})", hit.BlockPosition, hit.Face);

        // Simple instant dig for now (like creative/instabreak)
        // In survival, we'd need Start/Cancel/Finish sequence based on digging speed.
        // For interaction logic parity with Action, we just send start/finish.
        
        await _client.SendPacketAsync(new PlayerActionPacket
        {
            Status = PlayerActionPacket.StatusType.StartedDigging,
            Position = new Vector3<double>(hit.BlockPosition.X, hit.BlockPosition.Y, hit.BlockPosition.Z),
            Face = hit.Face,
            Sequence = entity.IncrementSequence()
        });
        
        await _client.SendPacketAsync(new PlayerActionPacket
        {
            Status = PlayerActionPacket.StatusType.FinishedDigging,
            Position = new Vector3<double>(hit.BlockPosition.X, hit.BlockPosition.Y, hit.BlockPosition.Z),
            Face = hit.Face,
            Sequence = entity.IncrementSequence()
        });

        await _client.SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });
        return true;
    }

    public async Task<bool> PlaceBlockAsync(Hand hand = Hand.MainHand)
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        if (entity.HeldItem.ItemId is null) return false;

        var hit = entity.GetLookingAtBlock(_client.State.Level, ReachDistance);
        if (hit is null) return false;

        var cursor = hit.GetInBlockPosition();
        
        _logger.LogInformation("Placing block at {Pos} facing {Face}", hit.BlockPosition, hit.Face);

        await _client.SendPacketAsync(new UseItemOnPacket
        {
            Hand = hand,
            Position = new Vector3<double>(hit.BlockPosition.X, hit.BlockPosition.Y, hit.BlockPosition.Z),
            BlockFace = hit.Face,
            Cursor = new Vector3<float>((float)cursor.X, (float)cursor.Y, (float)cursor.Z),
            InsideBlock = hit.InsideBlock,
            Sequence = entity.IncrementSequence()
        });
        
        await _client.SendPacketAsync(new SwingPacket { Hand = hand });
        return true;
    }

    public async Task<bool> InteractAsync(Hand hand = Hand.MainHand)
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        // 1. Try Entity Interaction
        // We replicate existing logic: Find entity in look direction.
        var allEntities = _client.State.Level.GetAllPlayers().Select(p => p.Entity).Where(e => e != null).Cast<Entity>()
            // Plus non-player entities which might need a different registry access if they exist
            // For now, let's assume Level.GetAllEntityIds -> GetEntityOfId covers all.
            .Concat(_client.State.Level.GetAllEntityIds().Select(id => _client.State.Level.GetEntityOfId(id)!));
            
        var targetEntity = entity.GetLookingAtEntity(entity, allEntities, ReachDistance);

        if (targetEntity != null)
        {
            _logger.LogInformation("Interacting with entity {Id}", targetEntity.EntityId);
            
            // Look at target (optional, but helpful for server validation)
            var yawPitch = entity.GetYawPitchToTarget(entity, targetEntity);
            await _client.SendPacketAsync(new MovePlayerRotationPacket
            {
                Yaw = yawPitch.X,
                Pitch = yawPitch.Y,
                Flags = MovementFlags.None
            });

            await _client.SendPacketAsync(new InteractPacket
            {
                EntityId = targetEntity.EntityId,
                Type = InteractType.Interact,
                Hand = hand,
                SneakKeyPressed = entity.IsSneaking
            });
            await _client.SendPacketAsync(new SwingPacket { Hand = hand });
            return true;
        }

        // 2. Fallback to Block Interaction
        var hit = entity.GetLookingAtBlock(_client.State.Level, ReachDistance);
        if (hit is null) return false;

        _logger.LogInformation("Interacting with block at {Pos}", hit.BlockPosition);

        var cursor = hit.GetInBlockPosition();
        await _client.SendPacketAsync(new UseItemOnPacket
        {
            Hand = hand,
            Position = new Vector3<double>(hit.BlockPosition.X, hit.BlockPosition.Y, hit.BlockPosition.Z),
            BlockFace = hit.Face,
            Cursor = new Vector3<float>((float)cursor.X, (float)cursor.Y, (float)cursor.Z),
            InsideBlock = hit.InsideBlock,
            Sequence = entity.IncrementSequence()
        });
        
        await _client.SendPacketAsync(new SwingPacket { Hand = hand });
        return true;
    }


    public async Task<bool> AttackAsync()
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        var allEntities = _client.State.Level.GetAllPlayers().Select(p => p.Entity).Where(e => e != null).Cast<Entity>()
            .Concat(_client.State.Level.GetAllEntityIds().Select(id => _client.State.Level.GetEntityOfId(id)!));

        var target = entity.GetLookingAtEntity(entity, allEntities, ReachDistance);
        if (target != null)
        {
            await AttackEntityAsync(target);
            return true;
        }

        return false;
    }

    public async Task AttackEntityAsync(Entity target)
    {
        var entity = _client.State.LocalPlayer.Entity;
        
        var yawPitch = entity.GetYawPitchToTarget(entity, target);
        await _client.SendPacketAsync(new MovePlayerRotationPacket
        {
            Yaw = yawPitch.X,
            Pitch = yawPitch.Y,
            Flags = MovementFlags.None
        });

        await _client.SendPacketAsync(new InteractPacket
        {
            EntityId = target.EntityId,
            Type = InteractType.Attack,
            SneakKeyPressed = entity.IsSneaking
        });
        await _client.SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });
    }

    public Task SwingHandAsync(Hand hand)
    {
        return _client.SendPacketAsync(new SwingPacket { Hand = hand });
    }

    public async Task<bool> DropHeldItemAsync()
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        if (entity.HeldItem.ItemId is null) return false;

        await _client.SendPacketAsync(new PlayerActionPacket
        {
            Status = PlayerActionPacket.StatusType.DropItemStack,
            Position = new Vector3<double>(0, 0, 0),
            Face = BlockFace.Bottom,
            Sequence = 0
        });
        
        // Update local inventory state immediately
        entity.Inventory.SetSlot((short)(entity.HeldSlot + 36), new Slot());
        return true;
    }

    public async Task<bool> SetHeldSlotAsync(short slot)
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        if (slot is < 0 or > 8) return false;

        await _client.SendPacketAsync(new SetCarriedItemPacket { Slot = slot });
        _client.State.LocalPlayer.Entity.HeldSlot = slot;
        return true;
    }
}
