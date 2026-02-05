using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Data;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Services;

public class InventoryManager(
    IPacketSender packetSender,
    ClientState state,
    ILogger<InventoryManager> logger,
    IItemRegistryService itemRegistry) : IInventoryManager
{
    public async Task<bool> EquipBestTool(BlockState block)
    {
        var inventory = state.LocalPlayer?.Entity?.Inventory;
        if (inventory == null)
        {
            logger.LogDebug("[EquipBestTool] Inventory is null");
            return false;
        }

        logger.LogDebug("[EquipBestTool] Scanning inventory for tool to break {Block} (Items count: {Count})", 
            block.Name, inventory.Items.Count);

        float bestSpeed = 1.0f;
        int bestSlot = -1;

        // Check all slots (excluding armor/crafting for now, focusing on 9-44 main inv + hotbar)
        // Inventory slots: 9-35 (Main), 36-44 (Hotbar)
        // We will scan hotbar and main inventory.
        
        // Use a local method to score slots
        void CheckSlot(int slotIndex, int? itemId)
        {
            if (itemId is null or 0)
            {
                // Empty slot, skip silently
                return;
            }
            
            var itemName = itemRegistry.GetItemName(itemId.Value);
            if (string.IsNullOrEmpty(itemName))
            {
                logger.LogTrace("[EquipBestTool] Slot {Slot}: ItemId {ItemId} has no name", slotIndex, itemId);
                return;
            }

            var toolType = ToolData.GetToolType(itemName);
            var tier = ToolData.GetToolTier(itemName);
            
            // Log ALL items with tool types
            if (toolType != ToolData.ToolType.None)
            {
                logger.LogDebug("[EquipBestTool] Slot {Slot}: Found tool {Item} (Type={Type}, Tier={Tier})", 
                    slotIndex, itemName, toolType, tier);
            }
            
            float speed = 1.0f;
            if (ToolData.IsCorrectTool(toolType, block))
            {
                speed = ToolData.GetSpeed(tier);
                logger.LogDebug("[EquipBestTool] Slot {Slot}: {Item} is CORRECT tool for {Block} (Speed={Speed})",
                    slotIndex, itemName, block.Name, speed);
            }
            else if (toolType != ToolData.ToolType.None)
            {
                logger.LogDebug("[EquipBestTool] Slot {Slot}: {Item} is NOT correct for {Block}",
                    slotIndex, itemName, block.Name);
            }
            
            if (speed > bestSpeed)
            {
                bestSpeed = speed;
                bestSlot = slotIndex;
                logger.LogDebug("[EquipBestTool] New best: slot {Slot} with speed {Speed}", slotIndex, speed);
            }
            else if (speed == bestSpeed && bestSlot != -1)
            {
                // Tie-breaker: Prefer higher tier if speed matches (unlikely unless non-effective) 
                // or prefer hotbar over inventory
                if (slotIndex >= 36 && slotIndex <= 44 && (bestSlot < 36 || bestSlot > 44))
                {
                    bestSlot = slotIndex;
                }
            }
        }

        // Scan Hotbar (36-44 in internal tracking, but held slot logic uses 0-8 for packets)
        // Inventory.Items uses container slots. 36-44 is hotbar.
        var slotKeys = inventory.Items.Keys.Where(k => k is >= 9 and <= 44).OrderBy(k => k).ToList();
        logger.LogDebug("[EquipBestTool] Slots 9-44 in inventory: [{Slots}]", string.Join(", ", slotKeys));
        
        foreach (var kvp in inventory.Items)
        {
            // Only checking main inventory and hotbar
            if (kvp.Key is >= 9 and <= 44) 
            {
                CheckSlot(kvp.Key, kvp.Value.ItemId);
            }
        }

        // If no tool found is better than hand (1.0f), we might just stick with current or select empty hand
        if (bestSlot == -1)
        {
            logger.LogDebug("[EquipBestTool] No better tool found, using hand");
            return true; 
        }
        
        logger.LogInformation("[EquipBestTool] Best tool in slot {Slot} with speed {Speed}", bestSlot, bestSpeed);

        // If best slot is in hotbar (36-44)
        if (bestSlot is >= 36 and <= 44)
        {
            int hotbarIndex = bestSlot - 36;
            await SetHotbarSlot(hotbarIndex);
            return true;
        }

        // If best slot is in main inventory (9-35), we need to swap it to hotbar.
        // For now, we'll implement a simple swap with the currently held slot.
        // TODO: Implement window click logic for swapping.
        // For this task, strict parity might require sophisticated window handling.
        // As a MVP fallback: We just warn we can't swap yet (requires implementing Container Transactions)
        // But since we are claiming parity, let's assume we will pick a hotbar slot to swap into.
        
        int targetHotbarSlot = inventory.HeldSlot; // Use current slot
        int targetHotbarContainerSlot = targetHotbarSlot + 36;
        
        logger.LogInformation("[InventoryManager] Found best tool in slot {Slot} (Speed {Speed}), swapping to hotbar {Hotbar}", bestSlot, bestSpeed, targetHotbarSlot);
        await SwapItems(bestSlot, targetHotbarContainerSlot);
        
        return true;
    }

    public async Task<bool> EquipItemMatches(IEnumerable<string> itemNames)
    {
        var validNames = itemNames.ToHashSet();
        var inventory = state.LocalPlayer?.Entity?.Inventory;
        if (inventory == null) return false;

        // Check if held item is already valid
        var held = inventory.HeldItem;
        if (held.ItemId.HasValue)
        {
            var heldName = itemRegistry.GetItemName(held.ItemId.Value);
            if (!string.IsNullOrEmpty(heldName) && validNames.Contains(heldName))
            {
                // Already equipped
                return true;
            }
        }

        // Scan Hotbar (36-44)
        for (int i = 36; i <= 44; i++)
        {
            if (inventory.Items.TryGetValue((short)i, out var item) && item.ItemId.HasValue)
            {
                var name = itemRegistry.GetItemName(item.ItemId.Value);
                if (!string.IsNullOrEmpty(name) && validNames.Contains(name))
                {
                    logger.LogInformation("[InventoryManager] Found matching item {Item} in hotbar slot {Slot}, equipping.", name, i);
                    await SetHotbarSlot(i - 36);
                    return true;
                }
            }
        }

        // Scan Main Inventory (9-35)
        foreach (var kvp in inventory.Items)
        {
            if (kvp.Key is >= 9 and <= 35)
            {
                var item = kvp.Value;
                if (item.ItemId.HasValue)
                {
                    var name = itemRegistry.GetItemName(item.ItemId.Value);
                    if (!string.IsNullOrEmpty(name) && validNames.Contains(name))
                    {
                        var targetHotbarSlot = inventory.HeldSlot;
                        var targetContainerSlot = targetHotbarSlot + 36;
                        logger.LogInformation("[InventoryManager] Found matching item {Item} in inventory slot {Slot}, swapping to hotbar {Hotbar}.", name, kvp.Key, targetHotbarSlot);
                        await SwapItems(kvp.Key, targetContainerSlot);
                        return true;
                    }
                }
            }
        }

        logger.LogWarning("[InventoryManager] No matching item found for request: [{Names}]", string.Join(", ", validNames));
        return false;
    }

    public async Task SetHotbarSlot(int hotbarSlot)
    {
        if (hotbarSlot < 0 || hotbarSlot > 8)
        {
            logger.LogWarning("[InventoryManager] Invalid hotbar slot: {Slot}", hotbarSlot);
            return;
        }
        
        var inventory = state.LocalPlayer?.Entity?.Inventory;
        if (inventory != null && inventory.HeldSlot == hotbarSlot)
        {
            logger.LogDebug("[InventoryManager] Already holding slot {Slot}", hotbarSlot);
            return; // Already held
        }

        logger.LogDebug("[InventoryManager] Switching to hotbar slot {Slot}", hotbarSlot);
        await packetSender.SendPacketAsync(new SetCarriedItemPacket
        {
            Slot = (short)hotbarSlot
        });
        
        if (inventory != null) inventory.HeldSlot = (short)hotbarSlot;
    }

    public async Task SwapItems(int fromSlot, int toSlot)
    {
        var inventory = state.LocalPlayer?.Entity?.Inventory;
        if (inventory == null) return;

        logger.LogInformation("[InventoryManager] Swapping slots {From} and {To}", fromSlot, toSlot);

        // If 'toSlot' is in the hotbar (36-44), we can use Mode 2 (Swap)
        if (toSlot >= 36 && toSlot <= 44 && (fromSlot < 36 || fromSlot > 44))
        {
            int hotbarIndex = toSlot - 36;
            
            await packetSender.SendPacketAsync(new ClickContainerPacket
            {
                WindowId = 0,
                StateId = inventory.StateId,
                Slot = (short)fromSlot,
                Button = (sbyte)hotbarIndex,
                Mode = ClickContainerMode.Swap,
                ChangedSlots = new(),
                CarriedItem = Slot.Empty
            });
            
            return;
        }

        // General Swap (Drag and Drop style): 
        // 1. Click 'fromSlot' to pick up
        // 2. Click 'toSlot' to place/swap
        
        // Pick up
        await packetSender.SendPacketAsync(new ClickContainerPacket
        {
            WindowId = 0,
            StateId = inventory.StateId,
            Slot = (short)fromSlot,
            Button = 0, // Left click
            Mode = ClickContainerMode.Pickup,
            ChangedSlots = new(),
            CarriedItem = Slot.Empty
        });

        // Small delay to let server process? Usually not needed if we track state correctly, 
        // but since we aren't predicting state changes yet, we might need to wait for the next packet.
        // However, for immediate UI response, we just fire and forget the second click with the same/incremented state.
        
        // Place down / Swap with target
        await packetSender.SendPacketAsync(new ClickContainerPacket
        {
            WindowId = 0,
            StateId = inventory.StateId, // Ideally this should be StateId + 1 if we are predicting
            Slot = (short)toSlot,
            Button = 0, // Left click
            Mode = ClickContainerMode.Pickup,
            ChangedSlots = new(),
            CarriedItem = inventory.GetSlot((short)fromSlot) // Emulate carried item
        });
    }

    public float GetDigSpeed(BlockState block)
    {
        var inventory = state.LocalPlayer?.Entity?.Inventory;
        if (inventory == null) return 1.0f;

        var heldItem = inventory.HeldItem;
        if (heldItem.ItemId == null) return 1.0f;

        var itemName = itemRegistry.GetItemName(heldItem.ItemId.Value);
        if (string.IsNullOrEmpty(itemName)) return 1.0f;
        
        var toolType = ToolData.GetToolType(itemName);
        
        float speed = 1.0f;
        if (ToolData.IsCorrectTool(toolType, block))
        {
            var tier = ToolData.GetToolTier(itemName);
            speed = ToolData.GetSpeed(tier);
        }
        
        // Effects (Haste etc) - TODO
        
        return speed;
    }

    public float GetBestDigSpeed(BlockState block)
    {
        var inventory = state.LocalPlayer?.Entity?.Inventory;
        if (inventory == null) return 1.0f;
        
        float bestSpeed = 1.0f;

        // Iterate all slots (main inventory + hotbar)
        foreach (var kvp in inventory.Items)
        {
            var item = kvp.Value;
            if (item.ItemId == null || item.ItemId <= 0 || item.ItemCount <= 0) continue;

            var name = itemRegistry.GetItemName(item.ItemId.Value);
            if (string.IsNullOrEmpty(name)) continue;

            var tier = ToolData.GetToolTier(name);
            var type = ToolData.GetToolType(name);

            if (ToolData.IsCorrectTool(type, block))
            {
                var speed = ToolData.GetSpeed(tier);
                if (speed > bestSpeed)
                {
                    bestSpeed = speed;
                }
            }
        }
        return bestSpeed;
    }
}
