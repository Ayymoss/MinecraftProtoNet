using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
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
public class EntityHandler(ILogger<EntityHandler> logger) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(EntityHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case AddEntityPacket addEntityPacket:
                // Only track player entities (Type 155 = Player in protocol 775)
                if (addEntityPacket.Type == Core.EntityTypes.Player)
                {
                    await client.State.Level.AddEntityAsync(
                        addEntityPacket.EntityUuid,
                        addEntityPacket.EntityId,
                        addEntityPacket.Position);
                }

                break;

            case RemoveEntitiesPacket removeEntitiesPacket:
                var entities = client.State.Level.GetAllEntityIds()
                    .Where(x => removeEntitiesPacket.Entities.Contains(x));
                foreach (var entityId in entities)
                {
                    await client.State.Level.RemoveEntityAsync(entityId);
                }

                break;

            case EntityPositionSyncPacket positionSyncPacket:
                await client.State.Level.SetPositionAsync(
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
                break;

            case MoveEntityPositionPacket moveEntityPositionPacket:
                await client.State.Level.UpdatePositionAsync(
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
