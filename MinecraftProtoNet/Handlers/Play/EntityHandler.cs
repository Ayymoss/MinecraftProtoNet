using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Services;

namespace MinecraftProtoNet.Handlers.Play;

/// <summary>
/// Handles entity movement and state packets.
/// </summary>
[HandlesPacket(typeof(AddEntityPacket))]
[HandlesPacket(typeof(RemoveEntitiesPacket))]
[HandlesPacket(typeof(EntityPositionSyncPacket))]
[HandlesPacket(typeof(MoveEntityPositionRotationPacket))]
[HandlesPacket(typeof(MoveEntityPositionPacket))]
[HandlesPacket(typeof(SetEntityMotionPacket))]
[HandlesPacket(typeof(HurtAnimationPacket))]
[HandlesPacket(typeof(SetHealthPacket))]
public class EntityHandler() : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(EntityHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case AddEntityPacket addEntityPacket:
                // Track player entities in PlayerRegistry
                bool isPlayer = addEntityPacket.Type == EntityTypes.Player;

                // Fallback: Check if UUID is known in PlayerRegistry (in case Type ID mismatch)
                if (!isPlayer)
                {
                     var p = client.State.Level.GetPlayerByUuid(addEntityPacket.EntityUuid);
                     if (p != null) isPlayer = true;
                }

                if (isPlayer)
                {
                    await client.State.Level.AddEntityAsync(
                        addEntityPacket.EntityUuid,
                        addEntityPacket.EntityId,
                        addEntityPacket.Position);
                }
                
                // Track ALL entities in WorldEntityRegistry for interaction purposes
                client.State.WorldEntities.AddEntity(
                    addEntityPacket.EntityId,
                    addEntityPacket.EntityUuid,
                    addEntityPacket.Type,
                    addEntityPacket.Position,
                    new Vector2<float>(addEntityPacket.Yaw, addEntityPacket.Pitch));

                break;

            case RemoveEntitiesPacket removeEntitiesPacket:
                // Remove from player registry
                var entities = client.State.Level.GetAllEntityIds()
                    .Where(x => removeEntitiesPacket.Entities.Contains(x));
                foreach (var entityId in entities)
                {
                    await client.State.Level.RemoveEntityAsync(entityId);
                }
                
                // Remove from world entity registry
                foreach (var entityId in removeEntitiesPacket.Entities)
                {
                    client.State.WorldEntities.RemoveEntity(entityId);
                }

                break;


            case EntityPositionSyncPacket positionSyncPacket:
                await client.State.Level.SetPositionAsync(
                    positionSyncPacket.EntityId,
                    positionSyncPacket.Position,
                    positionSyncPacket.Velocity,
                    positionSyncPacket.YawPitch,
                    positionSyncPacket.OnGround);
                // Also update WorldEntities
                client.State.WorldEntities.SetPosition(
                    positionSyncPacket.EntityId,
                    positionSyncPacket.Position,
                    positionSyncPacket.Velocity,
                    positionSyncPacket.YawPitch,
                    positionSyncPacket.OnGround);
                break;

            case MoveEntityPositionRotationPacket moveEntityPacket:
                await client.State.Level.UpdatePositionAsync(
                    moveEntityPacket.EntityId,
                    moveEntityPacket.Delta,
                    moveEntityPacket.OnGround);
                // Also update WorldEntities
                client.State.WorldEntities.UpdatePosition(
                    moveEntityPacket.EntityId,
                    moveEntityPacket.Delta,
                    moveEntityPacket.OnGround);
                break;

            case MoveEntityPositionPacket moveEntityPositionPacket:
                await client.State.Level.UpdatePositionAsync(
                    moveEntityPositionPacket.EntityId,
                    moveEntityPositionPacket.Delta,
                    moveEntityPositionPacket.OnGround);
                // Also update WorldEntities
                client.State.WorldEntities.UpdatePosition(
                    moveEntityPositionPacket.EntityId,
                    moveEntityPositionPacket.Delta,
                    moveEntityPositionPacket.OnGround);
                break;


            case SetEntityMotionPacket:
                // Server sends velocity for entities but clients typically don't use it
                // (except for projectiles and item entities)
                break;

            case HurtAnimationPacket hurtAnimationPacket:
                if (!client.State.LocalPlayer.HasEntity) break;
                client.State.LocalPlayer.Entity.HurtFromYaw = hurtAnimationPacket.Yaw;
                break;

            case SetHealthPacket setHealthPacket:
                if (!client.State.LocalPlayer.HasEntity) break;
                var localEntity = client.State.LocalPlayer.Entity;

                localEntity.Health = setHealthPacket.Health;
                localEntity.Hunger = setHealthPacket.Food;
                localEntity.HungerSaturation = setHealthPacket.FoodSaturation;

                if (setHealthPacket.Health <= 0)
                {
                    await client.SendPacketAsync(new ClientCommandPacket
                    {
                        ActionId = ClientCommandPacket.Action.PerformRespawn
                    });
                }

                break;
        }
    }
}
