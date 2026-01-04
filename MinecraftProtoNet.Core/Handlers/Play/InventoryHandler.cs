using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
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
[HandlesPacket(typeof(SetCursorItemPacket))]
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
                HandleSetContent(containerSetContentPacket, entity);
                break;

            case ContainerSetSlotPacket containerSetSlotPacket:
                HandleSetSlot(containerSetSlotPacket, entity);
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

            case SetCursorItemPacket setCursorItemPacket:
                entity.Inventory.CursorItem = setCursorItemPacket.Contents;
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleSetContent(ContainerSetContentPacket packet, State.Entity entity)
    {
        var slots = packet.SlotData
            .Select((x, i) => new { Index = (short)i, Slot = x })
            .ToDictionary(x => x.Index, x => x.Slot);

        // ContainerId 0 = player inventory
        if (packet.WindowId == 0)
        {
            entity.Inventory.StateId = packet.StateId;
            entity.Inventory.SetAllSlots(slots);
        }
        else if (entity.CurrentContainer?.ContainerId == packet.WindowId)
        {
            // Update container state
            entity.CurrentContainer.StateId = packet.StateId;
            entity.CurrentContainer.SetAllSlots(slots);
            
            // Sync player inventory slots from the container window
            // Player slots start after container slots
            var containerSlotCount = entity.CurrentContainer.Type.GetContainerSlotCount();
            
            foreach (var kvp in slots)
            {
                if (kvp.Key >= containerSlotCount)
                {
                    // Map window slot -> player inventory slot
                    // Formula: (WindowSlot - ContainerCount) + 9
                    // This maps the first player slot in window (Main Inv) to slot 9 in EntityInventory
                    // And typically Hotbar follows Main Inv, maintaining the +9 offset relation
                    short playerSlot = (short)(kvp.Key - containerSlotCount + 9);
                    entity.Inventory.SetSlot(playerSlot, kvp.Value);
                }
            }
        }
        else
        {
            logger.LogDebug("Received SetContent for unknown container {Id}", packet.WindowId);
        }
    }

    private void HandleSetSlot(ContainerSetSlotPacket packet, State.Entity entity)
    {
        // ContainerId -1 = cursor
        if (packet.WindowId == -1)
        {
            entity.Inventory.CursorItem = packet.Slot;
        }
        else if (packet.WindowId == 0)
        {
            entity.Inventory.StateId = packet.StateId;
            entity.Inventory.SetSlot(packet.SlotToUpdate, packet.Slot);
        }
        else if (entity.CurrentContainer?.ContainerId == packet.WindowId)
        {
            // Update container state
            entity.CurrentContainer.StateId = packet.StateId;
            entity.CurrentContainer.SetSlot(packet.SlotToUpdate, packet.Slot);
            
            // Sync player inventory if the slot is in the player section
            var containerSlotCount = entity.CurrentContainer.Type.GetContainerSlotCount();
            
            if (packet.SlotToUpdate >= containerSlotCount)
            {
                // Map window slot -> player inventory slot
                short playerSlot = (short)(packet.SlotToUpdate - containerSlotCount + 9);
                entity.Inventory.SetSlot(playerSlot, packet.Slot);
            }
        }
        else
        {
            logger.LogDebug("Received SetSlot for unknown container {Id}", packet.WindowId);
        }
    }
}

