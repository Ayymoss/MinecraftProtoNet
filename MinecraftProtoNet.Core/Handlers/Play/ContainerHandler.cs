using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Handlers.Play;

/// <summary>
/// Handles container/menu related packets (open screen, close, merchant offers).
/// </summary>
[HandlesPacket(typeof(OpenScreenPacket))]
[HandlesPacket(typeof(ContainerClosePacket))]
[HandlesPacket(typeof(MerchantOffersPacket))]
public class ContainerHandler(ILogger<ContainerHandler> logger) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(ContainerHandler));

    public Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        if (!client.State.LocalPlayer.HasEntity) return Task.CompletedTask;
        var entity = client.State.LocalPlayer.Entity;

        switch (packet)
        {
            case OpenScreenPacket openScreen:
                HandleOpenScreen(openScreen, entity);
                break;

            case ContainerClosePacket closePacket:
                HandleContainerClose(closePacket, entity);
                break;

            case MerchantOffersPacket merchantOffers:
                HandleMerchantOffers(merchantOffers, entity);
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleOpenScreen(OpenScreenPacket packet, Entity entity)
    {
        // Close any existing container
        if (entity.CurrentContainer?.IsOpen == true)
        {
            entity.CurrentContainer.Close();
        }

        // Create new container state
        var container = new ContainerState
        {
            ContainerId = packet.ContainerId,
            Type = packet.MenuType,
            Title = packet.Title,
            IsOpen = true
        };

        entity.CurrentContainer = container;
        entity.NotifyContainerOpened(container);

        logger.LogDebug("Opened container: {Type} (ID: {Id}) - \"{Title}\"", 
            packet.MenuType, packet.ContainerId, packet.Title);
    }

    private void HandleContainerClose(ContainerClosePacket packet, Entity entity)
    {
        if (entity.CurrentContainer?.ContainerId == packet.ContainerId)
        {
            logger.LogDebug("Closing container: {Id}", packet.ContainerId);
            entity.CurrentContainer.Close();
            entity.CurrentContainer = null;
        }
    }

    private void HandleMerchantOffers(MerchantOffersPacket packet, Entity entity)
    {
        if (entity.CurrentContainer?.ContainerId != packet.ContainerId)
        {
            logger.LogWarning("Received merchant offers for unknown container {Id}", packet.ContainerId);
            return;
        }

        if (entity.CurrentContainer.Type != MenuType.Merchant)
        {
            logger.LogWarning("Received merchant offers for non-merchant container type {Type}", 
                entity.CurrentContainer.Type);
            return;
        }

        entity.CurrentContainer.MerchantData = new MerchantState
        {
            Offers = packet.Offers,
            VillagerLevel = packet.VillagerLevel,
            VillagerXp = packet.VillagerXp,
            ShowProgress = packet.ShowProgress,
            CanRestock = packet.CanRestock
        };

        entity.CurrentContainer.NotifyChanged();

        logger.LogDebug("Received {Count} merchant offers for villager level {Level}", 
            packet.Offers.Count, packet.VillagerLevel);
    }
}
