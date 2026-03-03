using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;

namespace Bot.Webcore.Components.Pages.Components;

public partial class EntityList
{
    private string _searchFilter = string.Empty;


    private bool IsPathing => Bot.CustomGoalProcess?.IsActive() ?? false;

    protected override void OnInitialized()
    {
        Bot.OnStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Bot.OnStateChanged -= HandleStateChanged;
    }

    private void PathfindToEntity(WorldEntity entity)
    {
        var pos = new MinecraftProtoNet.Baritone.Api.Utils.BetterBlockPos(entity.Position.X, entity.Position.Y, entity.Position.Z);
        var goal = new MinecraftProtoNet.Baritone.Pathfinding.Goals.GoalNear(pos, 2);
        Bot.CustomGoalProcess?.SetGoalAndPath(goal);
        Bot.NotifyStateChanged();
    }

    private void StopPathfinding()
    {
        Bot.CustomGoalProcess?.OnLostControl();
        Bot.NotifyStateChanged();
    }

    private List<WorldEntity> GetFilteredEntities()
    {
        var entities = Bot.State.WorldEntities.GetAllEntities();
        var list = entities
            .OrderBy(GetDistanceToPlayer)
            .ToList();

        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            list = list
                .Where(e => GetEntityTypeName(e.EntityType)
                    .Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return list;
    }

    private string GetEntityTypeName(int entityTypeId)
    {
        if (ClientState.EntityTypeRegistry != null &&
            ClientState.EntityTypeRegistry.TryGetValue(entityTypeId, out var name))
        {
            return name.Replace("minecraft:", "");
        }

        return $"unknown ({entityTypeId})";
    }

    private double GetDistanceToPlayer(WorldEntity entity)
    {
        var playerPos = Bot.State.LocalPlayer?.Entity?.Position;
        if (playerPos == null) return double.MaxValue;

        var dx = entity.Position.X - playerPos.X;
        var dy = entity.Position.Y - playerPos.Y;
        var dz = entity.Position.Z - playerPos.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private string FormatDistance(WorldEntity entity)
    {
        var dist = GetDistanceToPlayer(entity);
        return dist < 10000 ? $"{dist:F1}m" : "???";
    }

    private string FormatPosition(WorldEntity entity)
    {
        return $"{entity.Position.X:F0}, {entity.Position.Y:F0}, {entity.Position.Z:F0}";
    }

    /// <summary>
    /// Calculates yaw/pitch from the local player's eye position to the target entity center.
    /// Reference: MinecraftProtoNet.Core/State/Entity.cs:401-416 (GetYawPitchToTarget)
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java (lookAt)
    /// </summary>
    private async Task LookAtEntity(WorldEntity entity)
    {
        var localEntity = Bot.State.LocalPlayer?.Entity;
        if (localEntity == null) return;

        // Eye position = position + (0, 1.62, 0)
        // Reference: Entity.cs PlayerEyeHeight = 1.62
        var eyeX = localEntity.Position.X;
        var eyeY = localEntity.Position.Y + 1.62;
        var eyeZ = localEntity.Position.Z;

        // Target center (approximate entity center = position + half height)
        var targetX = entity.Position.X;
        var targetY = entity.Position.Y + 0.9; // approximate center for most entities
        var targetZ = entity.Position.Z;

        var dx = targetX - eyeX;
        var dy = targetY - eyeY;
        var dz = targetZ - eyeZ;

        // Reference: Entity.GetYawPitchToTarget (Entity.cs:401-416)
        var yaw = (float)(Math.Atan2(-dx, dz) * (180.0 / Math.PI));
        var horizontalDistance = Math.Sqrt(dx * dx + dz * dz);
        var pitch = (float)(Math.Atan2(-dy, horizontalDistance) * (180.0 / Math.PI));

        yaw %= 360;
        if (yaw > 180) yaw -= 360;
        if (yaw <= -180) yaw += 360;
        pitch = Math.Clamp(pitch, -90, 90);

        // Update local entity state
        localEntity.YawPitch = new Vector2<float>(yaw, pitch);

        // Send rotation packet
        // Reference: InteractionManager.cs:390-396 (look-at before interact)
        await Bot.Client.SendPacketAsync(new MovePlayerRotationPacket
        {
            Yaw = yaw,
            Pitch = pitch,
            Flags = MovementFlags.None
        });

        Bot.NotifyStateChanged();
    }

    /// <summary>
    /// Looks at an entity then sends an interact packet + swing.
    /// Reference: InteractionManager.cs:390-409 (InteractAsync entity branch)
    /// Reference: minecraft-26.1-REFERENCE-ONLY LocalPlayer interaction flow
    /// </summary>
    private async Task InteractWithEntity(WorldEntity entity)
    {
        var localEntity = Bot.State.LocalPlayer?.Entity;
        if (localEntity == null) return;

        // Step 1: Look at the entity
        await LookAtEntity(entity);

        // Small delay to ensure rotation is processed server-side
        await Task.Delay(50);

        // Step 2: Send InteractPacket
        // Reference: InteractionManager.cs:398-405
        await Bot.Client.SendPacketAsync(new InteractPacket
        {
            EntityId = entity.EntityId,
            Type = InteractType.Interact,
            Hand = Hand.MainHand,
            SneakKeyPressed = localEntity.IsSneaking
        });

        // Step 3: Swing hand
        // Reference: InteractionManager.cs:407-409
        await Bot.Client.SendPacketAsync(new SwingPacket
        {
            Hand = Hand.MainHand
        });

        Bot.NotifyStateChanged();
    }
}
