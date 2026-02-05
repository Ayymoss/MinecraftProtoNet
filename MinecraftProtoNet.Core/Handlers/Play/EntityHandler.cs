using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Physics;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Core.Handlers.Play;

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
[HandlesPacket(typeof(DamageEventPacket))]
public class EntityHandler(ILogger<EntityHandler> logger, IPhysicsService physicsService) : IPacketHandler
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
                var isLocalPlayerSync = client.State.LocalPlayer.HasEntity && positionSyncPacket.EntityId == client.State.LocalPlayer.Entity?.EntityId;
                var oldPosSync = isLocalPlayerSync && client.State.LocalPlayer.Entity != null ? client.State.LocalPlayer.Entity.Position : Vector3<double>.Zero;
                var oldVelSync = isLocalPlayerSync && client.State.LocalPlayer.Entity != null ? client.State.LocalPlayer.Entity.Velocity : Vector3<double>.Zero;
                
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
                
                if (isLocalPlayerSync)
                {
                    logger.LogDebug("EntityPositionSyncPacket (LOCAL PLAYER): EntityId={EntityId}, OldPos={OldPos}, NewPos={NewPos}, OldVel={OldVel}, NewVel={NewVel}, OnGround={OnGround}",
                        positionSyncPacket.EntityId, oldPosSync, positionSyncPacket.Position, oldVelSync, positionSyncPacket.Velocity, positionSyncPacket.OnGround);
                }
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


            case SetEntityMotionPacket setEntityMotionPacket:
                // Server sends velocity updates for knockback, pushing, explosions, etc.
                // This is server-authoritative velocity that should override client-side physics.
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/player/Player.java:1082-1085
                var entity = client.State.Level.GetEntityOfId(setEntityMotionPacket.EntityId);
                if (entity != null)
                {
                    var oldVelocity = entity.Velocity;
                    var isLocalPlayer = client.State.LocalPlayer.HasEntity && entity.EntityId == client.State.LocalPlayer.Entity.EntityId;
                    // Apply server-sent velocity directly (in blocks/tick)
                    entity.Velocity = setEntityMotionPacket.Velocity;
                    logger.LogDebug("SetEntityMotionPacket: EntityId={EntityId}, IsLocalPlayer={IsLocalPlayer}, OldVel={OldVel}, NewVel={NewVel}",
                        setEntityMotionPacket.EntityId, isLocalPlayer, oldVelocity, setEntityMotionPacket.Velocity);
                }
                else
                {
                    logger.LogTrace("SetEntityMotionPacket: EntityId={EntityId} not found", setEntityMotionPacket.EntityId);
                }
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

            case DamageEventPacket damageEventPacket:
                // Apply knockback when entity takes damage
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:1197-1215
                // Try to get entity - check local player first, then Level registry
                Entity? damagedEntity = null;
                if (client.State.LocalPlayer.HasEntity && 
                    client.State.LocalPlayer.Entity?.EntityId == damageEventPacket.EntityId)
                {
                    damagedEntity = client.State.LocalPlayer.Entity;
                }
                else
                {
                    damagedEntity = client.State.Level.GetEntityOfId(damageEventPacket.EntityId);
                }
                
                if (damagedEntity == null) break;

                // Calculate knockback direction
                // Java logic: Try SourcePosition first, then SourceDirectId (attacker entity)
                double xd = 0.0;
                double zd = 0.0;
                
                if (damageEventPacket.SourcePosition != null)
                {
                    // Use source position if available
                    xd = damageEventPacket.SourcePosition.X - damagedEntity.Position.X;
                    zd = damageEventPacket.SourcePosition.Z - damagedEntity.Position.Z;
                }
                else if (damageEventPacket.SourceDirectId >= 0)
                {
                    // Try to get attacker entity position from SourceDirectId
                    // Check player entities first
                    var attackerEntity = client.State.Level.GetEntityOfId(damageEventPacket.SourceDirectId);
                    if (attackerEntity == null)
                    {
                        // Check world entities (non-player entities)
                        var attackerWorldEntity = client.State.WorldEntities.GetEntity(damageEventPacket.SourceDirectId);
                        if (attackerWorldEntity != null)
                        {
                            xd = attackerWorldEntity.Position.X - damagedEntity.Position.X;
                            zd = attackerWorldEntity.Position.Z - damagedEntity.Position.Z;
                        }
                    }
                    else
                    {
                        xd = attackerEntity.Position.X - damagedEntity.Position.X;
                        zd = attackerEntity.Position.Z - damagedEntity.Position.Z;
                    }
                }
                // If xd and zd are still 0.0, knockback() will use random direction (matches Java behavior)

                // Apply knockback with default power (0.4)
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:1211
                // TODO: Get knockback resistance from entity attributes when implemented
                physicsService.Knockback(damagedEntity, PhysicsConstants.DefaultKnockback, xd, zd, knockbackResistance: 0.0);
                break;
        }
    }
}
