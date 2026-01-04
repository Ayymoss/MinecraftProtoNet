using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base.Definitions;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// Tracks the state of an open container/menu (chest, villager, crafting table, etc.).
/// </summary>
public class ContainerState
{
    /// <summary>Unique ID for this container instance.</summary>
    public int ContainerId { get; set; }
    
    /// <summary>The type of menu, determining UI layout.</summary>
    public MenuType Type { get; set; }
    
    /// <summary>The title displayed at the top of the container UI.</summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>Whether this container is currently open.</summary>
    public bool IsOpen { get; set; }
    
    /// <summary>State ID for synchronization in ClickContainer transactions.</summary>
    public int StateId { get; set; }
    
    /// <summary>Container slots indexed by slot number.</summary>
    public Dictionary<short, Slot> Slots { get; } = new();
    
    /// <summary>Merchant/villager trading data (only set for Merchant type).</summary>
    public MerchantState? MerchantData { get; set; }

    /// <summary>Event fired when container contents change.</summary>
    public event Action? OnContainerChanged;
    
    /// <summary>Event fired when the container is closed.</summary>
    public event Action? OnContainerClosed;

    /// <summary>
    /// Gets the slot at the specified index.
    /// </summary>
    public Slot GetSlot(short slotIndex)
    {
        return Slots.TryGetValue(slotIndex, out var slot) ? slot : Slot.Empty;
    }

    /// <summary>
    /// Sets a single slot in the container.
    /// </summary>
    public void SetSlot(short slotIndex, Slot slot)
    {
        Slots[slotIndex] = slot;
        OnContainerChanged?.Invoke();
    }

    /// <summary>
    /// Replaces all slots with the given dictionary.
    /// </summary>
    public void SetAllSlots(Dictionary<short, Slot> slots)
    {
        Slots.Clear();
        foreach (var kvp in slots)
        {
            Slots[kvp.Key] = kvp.Value;
        }
        OnContainerChanged?.Invoke();
    }

    /// <summary>
    /// Closes this container and fires the closed event.
    /// </summary>
    public void Close()
    {
        IsOpen = false;
        Slots.Clear();
        MerchantData = null;
        OnContainerClosed?.Invoke();
    }

    /// <summary>
    /// Notifies listeners that the container state has changed.
    /// </summary>
    public void NotifyChanged() => OnContainerChanged?.Invoke();
}

/// <summary>
/// Merchant-specific trading state (villager/wandering trader).
/// </summary>
public class MerchantState
{
    /// <summary>Available trade offers.</summary>
    public List<MerchantOffer> Offers { get; set; } = new();
    
    /// <summary>Villager profession level (1-5).</summary>
    public int VillagerLevel { get; set; }
    
    /// <summary>Current villager XP towards next level.</summary>
    public int VillagerXp { get; set; }
    
    /// <summary>Whether to show the level progress bar.</summary>
    public bool ShowProgress { get; set; }
    
    /// <summary>Whether the merchant can restock trades.</summary>
    public bool CanRestock { get; set; }
    
    /// <summary>Currently selected trade index.</summary>
    public int SelectedTradeIndex { get; set; }
}
