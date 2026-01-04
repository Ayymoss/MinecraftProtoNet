using MinecraftProtoNet.Core.Packets.Base.Definitions;

namespace Bot.Webcore.Services;

/// <summary>
/// Shared drag/drop state for cross-component slot interactions.
/// Enables dragging between InventoryView and ContainerView components.
/// </summary>
public class DragDropState
{
    /// <summary>
    /// The source slot index being dragged from.
    /// </summary>
    public int? SourceSlot { get; private set; }
    
    /// <summary>
    /// True if dragging from remote container, false if from player inventory.
    /// </summary>
    public bool IsFromContainer { get; private set; }
    
    /// <summary>
    /// The item being dragged (for visual feedback).
    /// </summary>
    public Slot? DraggedItem { get; private set; }
    
    /// <summary>
    /// Whether a drag operation is in progress.
    /// </summary>
    public bool IsDragging => SourceSlot.HasValue;

    /// <summary>
    /// Event fired when drag state changes (for UI updates).
    /// </summary>
    public event Action? OnDragStateChanged;

    /// <summary>
    /// Start a drag operation from a slot.
    /// </summary>
    public void StartDrag(int slot, bool fromContainer, Slot item)
    {
        SourceSlot = slot;
        IsFromContainer = fromContainer;
        DraggedItem = item;
        OnDragStateChanged?.Invoke();
    }

    /// <summary>
    /// Clear the drag state (after drop or cancel).
    /// </summary>
    public void Clear()
    {
        SourceSlot = null;
        IsFromContainer = false;
        DraggedItem = null;
        OnDragStateChanged?.Invoke();
    }
}
