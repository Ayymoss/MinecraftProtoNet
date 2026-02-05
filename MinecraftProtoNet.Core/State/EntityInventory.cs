using MinecraftProtoNet.Core.Packets.Base.Definitions;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// Manages entity inventory state including held items and block placement sequences.
/// </summary>
public class EntityInventory
{
    private int _blockPlaceSequence;
    private int _stateId;
    
    /// <summary>
    /// The current state ID of the inventory, used for synchronization in ClickContainer transactions.
    /// </summary>
    public int StateId { get => _stateId; set => _stateId = value; }

    /// <summary>
    /// The item currently held by the mouse cursor (floating item).
    /// </summary>
    public Slot CursorItem { get; set; } = Slot.Empty;

    /// <summary>
    /// Event fired when inventory contents change.
    /// </summary>
    public event Action? OnInventoryChanged;
    
    /// <summary>
    /// The current block placement sequence number for anti-cheat.
    /// </summary>
    private static Services.IItemRegistryService? _registryService;

    /// <summary>
    /// Sets the registry service for item lookups. 
    /// Should be called during startup.
    /// </summary>
    public static void SetRegistryService(Services.IItemRegistryService service)
    {
        _registryService = service;
    }

    /// <summary>
    /// The current block placement sequence number for anti-cheat.
    /// </summary>
    public int BlockPlaceSequence => _blockPlaceSequence;

    /// <summary>
    /// Increments and returns the block placement sequence number.
    /// </summary>
    public int IncrementSequence()
    {
        var currentSequence = _blockPlaceSequence;
        Interlocked.Increment(ref _blockPlaceSequence);
        return currentSequence;
    }

    /// <summary>
    /// The currently selected hotbar slot (0-8).
    /// </summary>
    private short _heldSlot;
    public short HeldSlot
    {
        get => _heldSlot;
        set
        {
            if (_heldSlot != value)
            {
                _heldSlot = value;
                OnInventoryChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// The held slot index with container offset applied (36-44).
    /// </summary>
    public short HeldSlotWithOffset => (short)(HeldSlot + 36);

    /// <summary>
    /// Gets the currently held item.
    /// </summary>
    public Slot HeldItem => Items.TryGetValue(HeldSlotWithOffset, out var slot) ? slot : Slot.Empty;

    /// <summary>
    /// The entity's inventory items indexed by slot number.
    /// </summary>
    public Dictionary<short, Slot> Items { get; set; } = new();

    /// <summary>
    /// Gets an item from a specific slot.
    /// </summary>
    public Slot GetSlot(short slotIndex)
    {
        return Items.TryGetValue(slotIndex, out var slot) ? slot : Slot.Empty;
    }

    /// <summary>
    /// Sets an item in a specific slot.
    /// </summary>
    public void SetSlot(short slotIndex, Slot slot)
    {
        Items[slotIndex] = slot;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Clears all inventory slots.
    /// </summary>
    public void Clear()
    {
        Items.Clear();
        _blockPlaceSequence = 0;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Replaces all inventory items with the given dictionary.
    /// </summary>
    public void SetAllSlots(Dictionary<short, Slot> items)
    {
        Items = items;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Checks if the inventory contains any throwaway blocks suitable for pillaring or bridging.
    /// </summary>
    public bool HasThrowawayBlocks()
    {
        if (_registryService == null)
        {
            // Fallback if registry not loaded yet (shouldn't happen in normal run)
             foreach (var slot in Items.Values)
             {
                 if (slot.ItemId != null && slot.ItemId > 0 && slot.ItemCount > 0) return true;
             }
             return false;
        }

        foreach (var slot in Items.Values)
        {
            if (slot.ItemId == null || slot.ItemId <= 0 || slot.ItemCount <= 0) continue; 
            
            if (_registryService.IsThrowawayBlock(slot.ItemId.Value))
            {
                return true;
            }
        }
        return false;   
    }
}

