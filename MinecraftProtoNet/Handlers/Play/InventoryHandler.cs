using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Services;

namespace MinecraftProtoNet.Handlers.Play;

/// <summary>
/// Handles inventory-related packets.
/// </summary>
[HandlesPacket(typeof(ContainerSetContentPacket))]
[HandlesPacket(typeof(ContainerSetSlotPacket))]
[HandlesPacket(typeof(SetHeldSlotPacket))]
[HandlesPacket(typeof(BlockChangedAcknowledgementPacket))]
public class InventoryHandler(ILogger<InventoryHandler> logger) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(InventoryHandler));

    public Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        if (!client.State.LocalPlayer.HasEntity) return Task.CompletedTask;
        var entity = client.State.LocalPlayer.Entity;

        switch (packet)
        {
            case ContainerSetContentPacket containerSetContentPacket:
                entity.Inventory.SetAllSlots(containerSetContentPacket.SlotData
                    .Select((x, i) => new { Index = (short)i, Slot = x })
                    .ToDictionary(x => x.Index, x => x.Slot));
                break;

            case ContainerSetSlotPacket containerSetSlotPacket:
                entity.Inventory.SetSlot(containerSetSlotPacket.SlotToUpdate, containerSetSlotPacket.Slot);
                break;

            case SetHeldSlotPacket setHeldSlotPacket:
                entity.HeldSlot = setHeldSlotPacket.HeldSlot;
                break;

            case BlockChangedAcknowledgementPacket:
                entity.HeldItem.ItemCount -= 1;
                if (entity.HeldItem.ItemCount <= 0)
                {
                    entity.Inventory.SetSlot(entity.HeldSlotWithOffset, new Slot());
                }

                break;
        }

        return Task.CompletedTask;
    }
}
