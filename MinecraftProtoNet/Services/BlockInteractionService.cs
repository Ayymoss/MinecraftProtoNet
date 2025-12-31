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
/// Now uses tick-based breaking for natural timing (like holding left-click).
/// </summary>
public class BlockInteractionService(
    IPacketSender packetSender,
    ClientState state,
    IInventoryManager inventoryManager)
{
    private bool _isBreaking;
    private (int X, int Y, int Z)? _currentBreakPos;
    private int _breakTicksRemaining;
    private int _breakSequence;
    private bool _breakStartSent;

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
    /// Starts or continues breaking a block at the specified position.
    /// Call this every tick while breaking. Uses tick-based timing like vanilla.
    /// Returns true if block is broken or breaking in progress.
    /// </summary>
    public async Task<bool> BreakBlockAt(int x, int y, int z)
    {
        var newPos = (x, y, z);
        
        // If already breaking a different block, cancel it first
        if (_isBreaking && _currentBreakPos.HasValue && _currentBreakPos.Value != newPos)
        {
            await CancelBreaking();
        }

        // Check if target block is already air
        var targetBlock = state.Level.GetBlockAt(x, y, z);
        if (targetBlock.IsAir)
        {
            // Block already broken - reset state for next block
            if (_isBreaking && _currentBreakPos == newPos)
            {
                _isBreaking = false;
                _currentBreakPos = null;
                _breakStartSent = false;
            }
            return true;
        }

        // Start new break if not already breaking this block
        if (!_isBreaking || _currentBreakPos != newPos)
        {
            _isBreaking = true;
            _currentBreakPos = newPos;
            _breakStartSent = false;
            
            // Equip best tool
            await inventoryManager.EquipBestTool(targetBlock);
            
            // Calculate break time
            float speed = inventoryManager.GetDigSpeed(targetBlock);
            float hardness = targetBlock.DestroySpeed;
            bool canHarvest = true; // Simplified
            
            double durationTicks = ActionCosts.CalculateMiningDuration(speed, hardness, canHarvest);
            
            if (durationTicks >= ActionCosts.CostInf)
            {
                Log.Warning("Block {Block} is unbreakable", targetBlock.Name);
                _isBreaking = false;
                _currentBreakPos = null;
                return false;
            }
            
            _breakTicksRemaining = (int)Math.Ceiling(durationTicks);
            
            Log.Information("[BlockInteraction] Breaking block {Block} at ({X}, {Y}, {Z}), ticks: {Ticks}", 
                targetBlock.Name, x, y, z, _breakTicksRemaining);
        }

        // Send start packet if not yet sent
        if (!_breakStartSent)
        {
            var pos = new Vector3<double>(x, y, z);
            await packetSender.SendPacketAsync(new PlayerActionPacket
            {
                Status = PlayerActionPacket.StatusType.StartedDigging,
                Position = pos,
                Face = BlockFace.Top,
                Sequence = 0
            });
            await packetSender.SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });
            _breakStartSent = true;
        }

        // Decrement tick counter
        _breakTicksRemaining--;

        // Check if break complete
        if (_breakTicksRemaining <= 0)
        {
            // Send finish packet
            var pos = new Vector3<double>(x, y, z);
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

            // Reset ALL breaking state after sending FinishedDigging
            // This prevents double-send on next tick when block is still the target
            _isBreaking = false;
            _currentBreakPos = null;
            _breakStartSent = false;
        }

        return true;
    }

    /// <summary>
    /// Cancels any ongoing block breaking.
    /// </summary>
    public async Task CancelBreaking()
    {
        if (_isBreaking && _currentBreakPos.HasValue && _breakStartSent)
        {
            var pos = new Vector3<double>(_currentBreakPos.Value.X, _currentBreakPos.Value.Y, _currentBreakPos.Value.Z);
            await packetSender.SendPacketAsync(new PlayerActionPacket
            {
                Status = PlayerActionPacket.StatusType.CancelledDigging,
                Position = pos,
                Face = BlockFace.Top,
                Sequence = 0
            });
        }
        
        _isBreaking = false;
        _currentBreakPos = null;
        _breakTicksRemaining = 0;
        _breakStartSent = false;
    }

    /// <summary>
    /// Returns true if currently breaking a block.
    /// </summary>
    public bool IsBreaking => _isBreaking;

    /// <summary>
    /// Returns the current block being broken, if any.
    /// </summary>
    public (int X, int Y, int Z)? CurrentBreakTarget => _currentBreakPos;
}
