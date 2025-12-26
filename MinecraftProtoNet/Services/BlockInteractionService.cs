using Microsoft.Extensions.DependencyInjection;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Pathfinding;
using MinecraftProtoNet.State.Base;
using Serilog;

namespace MinecraftProtoNet.Services;

/// <summary>
/// Service for handling block interactions like placing and breaking blocks.
/// </summary>
public class BlockInteractionService(
    IPacketSender packetSender,
    ClientState state,
    IInventoryManager inventoryManager)
{
    private bool _isBreaking;
    private (int X, int Y, int Z)? _currentBreakPos;

    /// <summary>
    /// Attempts to place a block at the specified position.
    /// </summary>
    /// <param name="x">Target X coordinate</param>
    /// <param name="y">Target Y coordinate</param>
    /// <param name="z">Target Z coordinate</param>
    /// <returns>True if block was placed, false otherwise</returns>
    public async Task<bool> PlaceBlockAt(int x, int y, int z)
    {
        try
        {
            var entity = state.LocalPlayer?.Entity;
            if (entity == null) return false;
            
            var inventory = entity.Inventory;

            // Find a throwaway block in hotbar (slots 36-44 are hotbar in player inventory)
            // SetCarriedItemPacket expects 0-8 though
            short? hotbarSlot = null;
            
            // First check if currently held item is a placeable block
            var heldItem = inventory.HeldItem;
            if (heldItem.ItemId is > 0 && heldItem.ItemCount > 0)
            {
                // Current held item is valid - use it
                hotbarSlot = inventory.HeldSlot;
                Log.Debug("[BlockInteraction] Using currently held item: {ItemId} in slot {Slot}", heldItem.ItemId, hotbarSlot);
            }
            else
            {
                // Search hotbar for a block (slots 36-44 in inventory = 0-8 for SetCarriedItem)
                for (short slot = 0; slot < 9; slot++)
                {
                    var inventorySlot = (short)(slot + 36); // Convert to inventory index
                    var item = inventory.GetSlot(inventorySlot);
                    if (item.ItemId is > 0 && item.ItemCount > 0)
                    {
                        hotbarSlot = slot;
                        Log.Debug("[BlockInteraction] Found item {ItemId} at hotbar slot {Slot} (inv slot {InvSlot})", 
                            item.ItemId, slot, inventorySlot);
                        break;
                    }
                }
            }

            if (hotbarSlot == null)
            {
                Log.Warning("[BlockInteraction] No blocks in hotbar to place! Items count: {Count}", inventory.Items.Count);
                return false;
            }

            // Switch to the slot with block
            await packetSender.SendPacketAsync(new SetCarriedItemPacket
            {
                Slot = hotbarSlot.Value
            });

            // Place the block - we place ON the block below (y-1), face UP
            var targetBlockPos = new Vector3<double>(x, y - 1, z);
            var cursorPos = new Vector3<float>(0.5f, 1.0f, 0.5f); // Center of top face

            // Increment block place sequence for anti-cheat
            var sequence = inventory.IncrementSequence();

            await packetSender.SendPacketAsync(new UseItemOnPacket
            {
                Hand = Hand.MainHand,
                Position = targetBlockPos,
                BlockFace = BlockFace.Top,
                Cursor = cursorPos,
                InsideBlock = false,
                Sequence = sequence
            });

            // Swing arm for animation
            await packetSender.SendPacketAsync(new SwingPacket
            {
                Hand = Hand.MainHand
            });

            Log.Debug("[BlockInteraction] Placed block at ({X}, {Y}, {Z}) from hotbar slot {Slot}", x, y, z, hotbarSlot);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[BlockInteraction] Block placement error");
            return false;
        }
    }

    /// <summary>
    /// Attempts to break a block at the specified position.
    /// Handles tool selection, mining duration, and packet sequence.
    /// </summary>
    /// <summary>
    /// Attempts to break a block at the specified position.
    /// Handles tool selection, mining duration, and packet sequence.
    /// </summary>
    public async Task<bool> BreakBlockAt(int x, int y, int z)
    {
        if (_isBreaking)
        {
            if (_currentBreakPos.HasValue && _currentBreakPos.Value.X == x && _currentBreakPos.Value.Y == y && _currentBreakPos.Value.Z == z)
            {
                // Already breaking this block
                return true;
            }
            // Busy breaking another block
            Log.Debug("[BlockInteraction] Busy breaking another block at {Current}, ignoring request for {New}", _currentBreakPos, (x, y, z));
            return false;
        }

        try
        {
            _isBreaking = true;
            _currentBreakPos = (x, y, z);

            var targetBlock = state.Level.GetBlockAt(x, y, z);
            if (targetBlock.IsAir) return true; // Already air

            Log.Information("[BlockInteraction] Breaking block {Block} at ({X}, {Y}, {Z})", targetBlock.Name, x, y, z);

            // 1. Equip Best Tool
            await inventoryManager.EquipBestTool(targetBlock);

            // 2. Calculate Break Time
            float speed = inventoryManager.GetDigSpeed(targetBlock);
            float hardness = targetBlock.DestroySpeed;
            bool canHarvest = true; // Simplified
            
            double durationTicks = ActionCosts.CalculateMiningDuration(speed, hardness, canHarvest);
            
            if (durationTicks >= ActionCosts.CostInf)
            {
                Log.Warning("Block {Block} is unbreakable", targetBlock.Name);
                return false;
            }

            // 3. Start Digging
            var pos = new Vector3<double>(x, y, z); 
            
            await packetSender.SendPacketAsync(new PlayerActionPacket
            {
                Status = PlayerActionPacket.StatusType.StartedDigging,
                Position = pos,
                Face = BlockFace.Top,
                Sequence = 0 // Usually 0 unless predicting sequences
            });
            await packetSender.SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });

            // 4. Wait
            if (durationTicks > 0)
            {
                int ms = (int)Math.Ceiling(durationTicks * 50); // 50ms per tick
                await Task.Delay(ms);
            }

            // 5. Finish Digging
            int sequence = 0;
            if (state.LocalPlayer?.Entity?.Inventory != null)
            {
                sequence = state.LocalPlayer.Entity.Inventory.IncrementSequence();
            }

            await packetSender.SendPacketAsync(new PlayerActionPacket
            {
                Status = PlayerActionPacket.StatusType.FinishedDigging,
                Position = pos,
                Face = BlockFace.Top,
                Sequence = sequence
            });
            
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[BlockInteraction] Break block error");
            return false;
        }
        finally
        {
            _isBreaking = false;
            _currentBreakPos = null;
        }
    }
}
