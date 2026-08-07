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
using MinecraftProtoNet.Core.State.Base;

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
[HandlesPacket(typeof(SetEntityDataPacket))]
[HandlesPacket(typeof(UpdateAttributesPacket))]
[HandlesPacket(typeof(UpdateMobEffectPacket))]
[HandlesPacket(typeof(RemoveMobEffectPacket))]
public class EntityHandler(ILogger<EntityHandler> logger, IPhysicsService physicsService) : IPacketHandler
{
    /// <summary>
    /// Damage types that should NOT apply knockback.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/data/minecraft/tags/damage_type/no_knockback.json
    /// </summary>
    private static readonly HashSet<string> NoKnockbackDamageTypes =
    [
        "minecraft:explosion", "minecraft:player_explosion", "minecraft:bad_respawn_point",
        "minecraft:in_fire", "minecraft:lightning_bolt", "minecraft:on_fire",
        "minecraft:lava", "minecraft:hot_floor", "minecraft:in_wall",
        "minecraft:cramming", "minecraft:drown", "minecraft:starve",
        "minecraft:cactus", "minecraft:fall", "minecraft:ender_pearl",
        "minecraft:fly_into_wall", "minecraft:out_of_world", "minecraft:generic",
        "minecraft:magic", "minecraft:wither", "minecraft:dragon_breath",
        "minecraft:dry_out", "minecraft:sweet_berry_bush", "minecraft:freeze",
        "minecraft:stalagmite", "minecraft:outside_border", "minecraft:generic_kill",
        "minecraft:campfire", "minecraft:spear"
    ];
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(EntityHandler));

    /// <summary>
    /// Combines an attribute's base value with its modifiers, in vanilla's order: all ADD_VALUE first, then
    /// ADD_MULTIPLIED_BASE against the post-add base, then ADD_MULTIPLIED_TOTAL against the running total.
    /// Order matters — the three operations are not commutative.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/entity/ai/attributes/AttributeInstance.java:150-168
    /// Operation ordinals: 0 ADD_VALUE, 1 ADD_MULTIPLIED_BASE, 2 ADD_MULTIPLIED_TOTAL (AttributeModifier.java:29-31)
    /// </summary>
    private static double CombineAttribute(UpdateAttributesPacket.Property property)
    {
        // The sprinting modifier is deliberately excluded. The server applies it to its own copy and broadcasts
        // the result, but the client owns this one: setSprinting() removes it and re-adds it locally every time
        // the state changes, and the speed calculation then applies it. Keeping the server's copy as well
        // multiplies it in twice (0.1 -> 0.13 -> 0.169), producing ~1.3x the legal sprint speed — which a
        // server with movement checks answers by setting the player back.
        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2238-2246
        // (id "minecraft:sprinting", +0.3 ADD_MULTIPLIED_TOTAL, LivingEntity.java:152 and :3974)
        var modifiers = property.Modifiers
            .Where(m => m.Identifier is not ("minecraft:sprinting" or "sprinting"))
            .ToArray();

        var baseValue = property.Value;
        foreach (var modifier in modifiers)
        {
            if (modifier.Operation == 0) baseValue += modifier.Amount;
        }

        var result = baseValue;
        foreach (var modifier in modifiers)
        {
            if (modifier.Operation == 1) result += baseValue * modifier.Amount;
        }

        foreach (var modifier in modifiers)
        {
            if (modifier.Operation == 2) result *= 1.0 + modifier.Amount;
        }

        return result;
    }

    private static Entity? ResolveEntity(IMinecraftClient client, int entityId)
    {
        var localPlayer = client.State.LocalPlayer;
        if (localPlayer.HasEntity && localPlayer.Entity.EntityId == entityId)
        {
            return localPlayer.Entity;
        }
        return client.State.Level.GetEntityOfId(entityId);
    }

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case UpdateMobEffectPacket updateMobEffect:
                ResolveEntity(client, updateMobEffect.EntityId)?.AddEffect(updateMobEffect.EffectId, updateMobEffect.Amplifier);
                break;

            case RemoveMobEffectPacket removeMobEffect:
                ResolveEntity(client, removeMobEffect.EntityId)?.RemoveEffect(removeMobEffect.EffectId);
                break;

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

            case SetEntityDataPacket setEntityDataPacket:
                // Extract health from metadata index 9 (LivingEntity.DATA_HEALTH_ID)
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java
                foreach (var metadata in setEntityDataPacket.MetadataPayload)
                {
                    // Retain every field verbatim (the vanilla client keeps the whole SynchedEntityData set).
                    // Identity fields — custom name, name visibility, invisibility — are decoded by the registry.
                    client.State.WorldEntities.SetMetadata(
                        setEntityDataPacket.EntityId,
                        metadata.Index,
                        metadata.Type is { } metadataType ? (int)metadataType : -1,
                        metadata.Value);

                    if (metadata is { Index: 9, Type: SetEntityDataPacket.MetadataType.Float, Value: float health })
                    {
                        // Update player entity health
                        var playerEntity = client.State.Level.GetEntityOfId(setEntityDataPacket.EntityId);
                        playerEntity?.Health = health;

                        // Update world entity health
                        client.State.WorldEntities.UpdateHealth(setEntityDataPacket.EntityId, health);
                    }
                }
                break;

            case UpdateAttributesPacket updateAttributesPacket:
                // Extract max_health attribute
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/ai/attributes/Attributes.java
                {
                    // Attribute ids come from the STATIC registry, not RegistryKeyOrder: minecraft:attribute is
                    // a built-in registry that servers never send, so the old lookup against server-sent
                    // registry data never matched and this whole handler was dead code — max_health included.
                    var attributes = ClientState.AttributeRegistry;

                    // The local player is not in the Level entity registry, so resolve it explicitly —
                    // otherwise the one entity whose speed actually drives our physics is the one we skip.
                    var targetEntity = ResolveEntity(client, updateAttributesPacket.EntityId);

                    foreach (var property in updateAttributesPacket.Properties)
                    {
                        var attributeName = attributes.GetValueOrDefault(property.Id);
                        if (attributeName == "minecraft:max_health")
                        {
                            var maxHealth = (float)property.Value;
                            if (targetEntity is not null) targetEntity.MaxHealth = maxHealth;
                            client.State.WorldEntities.UpdateMaxHealth(updateAttributesPacket.EntityId, maxHealth);
                        }
                        else if (attributeName == "minecraft:movement_speed" && targetEntity is not null)
                        {
                            var speed = CombineAttribute(property);
                            if (Math.Abs(speed - targetEntity.MovementSpeed) > 1.0E-9)
                            {
                                logger.LogInformation(
                                    "Movement speed for entity {EntityId}: {Old:F4} -> {New:F4} (base {Base:F4}, modifiers: {Modifiers})",
                                    updateAttributesPacket.EntityId, targetEntity.MovementSpeed, speed,
                                    property.Value,
                                    property.Modifiers.Length == 0
                                        ? "<none>"
                                        : string.Join(" | ", property.Modifiers.Select(m =>
                                            $"{m.Identifier} amount={m.Amount:F4} op={m.Operation}")));
                            }
                            targetEntity.MovementSpeed = speed;
                        }
                    }
                }
                break;

            case DamageEventPacket damageEventPacket:
                // Apply knockback when entity takes damage
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:1197-1215

                // Check NO_KNOCKBACK tag — many damage types (fall, fire, drown, etc.) should not cause knockback
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:1197
                // Java: if (!source.is(DamageTypeTags.NO_KNOCKBACK))
                if (client.State.RegistryKeyOrder.TryGetValue("minecraft:damage_type", out var damageTypes) &&
                    damageEventPacket.SourceTypeId >= 0 && damageEventPacket.SourceTypeId < damageTypes.Count)
                {
                    var damageTypeName = damageTypes[damageEventPacket.SourceTypeId];
                    if (NoKnockbackDamageTypes.Contains(damageTypeName))
                    {
                        logger.LogDebug("DamageEventPacket: Skipping knockback for NO_KNOCKBACK damage type {DamageType}", damageTypeName);
                        break;
                    }
                }

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
