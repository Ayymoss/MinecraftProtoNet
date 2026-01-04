/*
 * This file is part of Baritone.
 *
 * Baritone is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Baritone is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with Baritone.  If not, see <https://www.gnu.org/licenses/>.
 *
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Behavior;
using MinecraftProtoNet.Baritone.Api.Event.Events;
using MinecraftProtoNet.Baritone.Core;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Models.Json;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.State;
using BaritoneImpl = MinecraftProtoNet.Baritone.Core.Baritone;

namespace MinecraftProtoNet.Baritone.Behaviors;

/// <summary>
/// Inventory behavior implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java
/// </summary>
public class InventoryBehavior(IBaritone baritone) : Behavior(baritone), IInventoryBehavior
{
    private int _ticksSinceLastInventoryMove;
    private int[]? _lastTickRequestedMove;

    public override void OnTick(TickEvent evt)
    {
        if (!Core.Baritone.Settings().AllowInventory.Value)
        {
            return;
        }
        if (evt.GetType() == TickEvent.TickEventType.Out)
        {
            return;
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:64-67
        // Check if container menu is open
        var player = Baritone.GetPlayerContext().Player() as Entity;
        if (player != null && player.CurrentContainer != null)
        {
            // Container is open, don't manage inventory
            return;
        }
        
        _ticksSinceLastInventoryMove++;
        
        // Move throwaway items to hotbar slot 8
        int throwaway = FirstValidThrowaway();
        if (throwaway >= 9)
        {
            RequestSwapWithHotBar(throwaway, 8);
        }
        
        // Move best tool (pickaxe) to hotbar slot 0
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:72-75
        int pick = BestToolAgainst("minecraft:stone");
        if (pick >= 9)
        {
            RequestSwapWithHotBar(pick, 0);
        }
        
        if (_lastTickRequestedMove != null)
        {
            RequestSwapWithHotBar(_lastTickRequestedMove[0], _lastTickRequestedMove[1]);
        }
    }

    public void Move(int from, int to)
    {
        RequestSwapWithHotBar(from, to);
    }

    public void Close()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java
        // Close inventory/container - for headless client this may not be applicable
        // TODO: Implement if needed for container management
    }

    private bool RequestSwapWithHotBar(int inInventory, int inHotbar)
    {
        _lastTickRequestedMove = [inInventory, inHotbar];
        if (_ticksSinceLastInventoryMove < MinecraftProtoNet.Baritone.Core.Baritone.Settings().TicksBetweenInventoryMoves.Value)
        {
            return false;
        }
        if (BaritoneImpl.Settings().InventoryMoveOnlyIfStationary.Value)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:119-122
            var pauser = ((BaritoneImpl)Baritone).GetInventoryPauserProcess();
            if (pauser != null && !pauser.StationaryForInventoryMove())
            {
                return false;
            }
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:123
        // Execute inventory swap
        var player = Baritone.GetPlayerContext().Player() as Entity;
        if (player != null)
        {
            // Convert slot indices: inInventory < 9 means it's already in hotbar (0-8), otherwise it's in main inventory (9-35)
            // Hotbar slots in container: 36-44
            // Main inventory slots in container: 9-35
            int containerSlot = inInventory < 9 ? inInventory + 36 : inInventory;
            int hotbarSlot = inHotbar;
            
            // TODO: Implement window click for inventory swap when container system is available
            // For now, directly swap in the inventory
            var temp = player.Inventory.GetSlot((short)containerSlot);
            player.Inventory.SetSlot((short)containerSlot, player.Inventory.GetSlot((short)(hotbarSlot + 36)));
            player.Inventory.SetSlot((short)(hotbarSlot + 36), temp);
        }
        
        _ticksSinceLastInventoryMove = 0;
        _lastTickRequestedMove = null;
        return true;
    }

    private int FirstValidThrowaway()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:129-137
        var player = Baritone.GetPlayerContext().Player() as Entity;
        if (player == null) return -1;
        
        var settings = Core.Baritone.Settings();
        var acceptableItems = settings.AcceptableThrowawayItems.Value;
        
        // Check all inventory slots (0-35 for main inventory, 36-44 for hotbar)
        for (int i = 0; i < 36; i++)
        {
            var slot = player.Inventory.GetSlot((short)i);
            if (slot.ItemId != null && slot.ItemCount > 0)
            {
                // TODO: Check if item name matches acceptable throwaway items using item registry
                // For now, check if it's a common throwaway block
                // This will be enhanced when item registry is available
            }
        }
        
        return -1;
    }

    /// <summary>
    /// Checks if the inventory has generic throwaway items.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:162-169
    /// </summary>
    public bool HasGenericThrowaway()
    {
        var player = Baritone.GetPlayerContext().Player() as Entity;
        if (player == null) return false;
        
        var settings = Core.Baritone.Settings();
        var acceptableItems = settings.AcceptableThrowawayItems.Value;
        
        // Check if any acceptable throwaway items are in the inventory
        foreach (var itemName in acceptableItems)
        {
            if (Throwaway(false, slot =>
            {
                // TODO: Check if slot item matches itemName using item registry
                // For now, return false
                return false;
            }))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Selects a throwaway item for the specified location.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:171-185
    /// </summary>
    public bool SelectThrowawayForLocation(bool select, int x, int y, int z)
    {
        var builderProcess = ((BaritoneImpl)Baritone).GetBuilderProcess();
        var bsi = ((BaritoneImpl)Baritone).Bsi;
        if (bsi == null) return false;
        
        var currentState = bsi.Get0(x, y, z);
        // Convert World.Chunk.BlockState to Json.BlockState for interface
        var jsonBlockState = currentState != null 
            ? new BlockState { Id = currentState.Id, Properties = currentState.Properties }
            : null;
        var maybe = builderProcess?.PlaceAt(x, y, z, jsonBlockState!);
        
        if (maybe != null)
        {
            // Try to find exact block match first
            if (Throwaway(select, slot =>
            {
                // TODO: Check if slot contains the exact block needed using item registry
                return false;
            }))
            {
                return true;
            }
            
            // Try to find block type match
            if (Throwaway(select, slot =>
            {
                // TODO: Check if slot contains block of same type using item registry
                return false;
            }))
            {
                return true;
            }
        }
        
        // Fall back to generic throwaway items
        var settings = Core.Baritone.Settings();
        foreach (var itemName in settings.AcceptableThrowawayItems.Value)
        {
            if (Throwaway(select, slot =>
            {
                // TODO: Check if slot item matches itemName using item registry
                return false;
            }))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Finds and selects a throwaway item matching the predicate.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:187-238
    /// </summary>
    private bool Throwaway(bool select, Func<Slot, bool> desired)
    {
        return Throwaway(select, desired, Core.Baritone.Settings().AllowInventory.Value);
    }

    private bool Throwaway(bool select, Func<Slot, bool> desired, bool allowInventory)
    {
        var player = Baritone.GetPlayerContext().Player() as Entity;
        if (player == null) return false;
        
        // Check hotbar first (slots 36-44, which are 0-8 in hotbar indexing)
        for (int i = 0; i < 9; i++)
        {
            var slot = player.Inventory.GetSlot((short)(i + 36));
            if (desired(slot))
            {
                if (select)
                {
                    player.HeldSlot = (short)i;
                }
                return true;
            }
        }
        
        // Check offhand if needed
        // TODO: Implement offhand checking when available
        
        if (allowInventory)
        {
            // Check main inventory (slots 9-35)
            for (int i = 9; i < 36; i++)
            {
                var slot = player.Inventory.GetSlot((short)i);
                if (desired(slot))
                {
                    if (select)
                    {
                        RequestSwapWithHotBar(i, 7);
                        player.HeldSlot = 7;
                    }
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Finds the best tool against a block.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:139-160
    /// </summary>
    private int BestToolAgainst(string blockName)
    {
        var player = Baritone.GetPlayerContext().Player() as Entity;
        if (player == null) return -1;
        
        int bestInd = -1;
        double bestSpeed = -1;
        var blockState = new MinecraftProtoNet.Core.Models.World.Chunk.BlockState(0, blockName, new Dictionary<string, string>());
        
        // Check all inventory slots
        for (int i = 0; i < 36; i++)
        {
            var slot = player.Inventory.GetSlot((short)i);
            if (slot.ItemId == null || slot.ItemCount <= 0)
            {
                continue;
            }
            
            // Check item saver setting
            var settings = Core.Baritone.Settings();
            if (settings.ItemSaver.Value)
            {
                // TODO: Check item damage when component system is available
            }
            
            // Check if item is a tool and calculate speed
            double speed = ToolSet.CalculateSpeedVsBlock(slot, blockState);
            if (speed > bestSpeed)
            {
                bestSpeed = speed;
                bestInd = i;
            }
        }
        
        return bestInd;
    }
}

