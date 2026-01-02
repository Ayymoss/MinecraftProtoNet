using Microsoft.AspNetCore.Components.Web;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base.Definitions;

namespace Bot.Webcore.Components.Pages.Components;

public partial class InventoryView
{
    protected override void OnInitialized()
    {
        Bot.OnStateChanged += HandleStateChanged;
        DragState.OnDragStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private string GetSlotClass(int slotIndex, int heldSlot, bool isHeld = false, bool isDragging = false)
    {
        var baseClass =
            "w-full aspect-square rounded-md flex flex-col items-center justify-center p-0.5 transition-all duration-150 overflow-hidden min-h-[48px] ";

        if (isDragging)
            return baseClass + "bg-blue-900/50 border-2 border-blue-400 opacity-50";

        if (isHeld)
            return baseClass + "bg-emerald-900/50 border-2 border-emerald-400 hover:bg-emerald-800/50";

        return baseClass + "bg-slate-800/80 border border-slate-600/50 hover:border-slate-500 hover:bg-slate-700/80";
    }

    private string GetItemName(int itemId)
    {
        var name = Bot.ItemRegistry.GetItemName(itemId);
        if (string.IsNullOrEmpty(name)) return $"#{itemId}";

        // Strip "minecraft:" prefix and format nicely
        return name.Replace("minecraft:", "").Replace("_", " ");
    }

    private string GetFullItemName(int? itemId)
    {
        if (!itemId.HasValue) return "Empty";
        var name = Bot.ItemRegistry.GetItemName(itemId.Value);
        return string.IsNullOrEmpty(name) ? $"Item #{itemId}" : name.Replace("minecraft:", "").Replace("_", " ");
    }

    private async Task OnHotbarClick(int slotIndex, MouseEventArgs e)
    {
        if (e.ShiftKey)
        {
            // Shift-click: quick move to container (if open)
            await QuickMoveSlot(slotIndex);
        }
        else
        {
            // Normal click: select hotbar slot
            await SelectHotbarSlot(slotIndex - 36);
        }
    }

    private async Task OnSlotClick(int slotIndex, MouseEventArgs e)
    {
        if (e.ShiftKey)
        {
            // Shift-click: quick move to container (if open)
            await QuickMoveSlot(slotIndex);
        }
    }

    private async Task QuickMoveSlot(int slotIndex)
    {
        if (Bot.ContainerManager?.IsContainerOpen == true)
        {
            // When container is open, inventory slots are part of the container window
            // We need to use the container's window ID with the inventory slot offset
            var container = Bot.ContainerManager.CurrentContainer!;
            var containerSlotCount = container.Type.GetContainerSlotCount();

            // Container slots are 0 to containerSlotCount-1
            // Player inventory slots follow, starting at containerSlotCount
            // Map inventory slot indices (9-44) to container window slot indices
            short containerSlotIndex = (short)(containerSlotCount + slotIndex - 9);

            await Bot.ContainerManager.QuickMoveSlotAsync(containerSlotIndex, container.ContainerId);
            Bot.NotifyStateChanged();
        }
    }

    private async Task SelectHotbarSlot(int hotbarSlot)
    {
        await Bot.InventoryManager.SetHotbarSlot(hotbarSlot);
        Bot.NotifyStateChanged();
    }

    private void OnDragStart(int slotIndex, Slot slot)
    {
        DragState.StartDrag(slotIndex, fromContainer: false, slot);
    }

    private async Task OnDrop(int targetSlot)
    {
        if (!DragState.IsDragging) return;

        if (DragState.IsFromContainer)
        {
            // Dropping from container to inventory - need to handle via container clicks
            if (Bot.ContainerManager?.IsContainerOpen == true)
            {
                var container = Bot.ContainerManager.CurrentContainer!;
                var containerSlotCount = container.Type.GetContainerSlotCount();

                // First, pick up from container slot
                await Bot.ContainerManager.ClickSlotAsync((short)DragState.SourceSlot!.Value);

                // Then, put down in inventory slot (mapped to container window slot)
                short containerTargetSlot = (short)(containerSlotCount + targetSlot - 9);
                await Bot.ContainerManager.ClickSlotAsync(containerTargetSlot);

                Bot.NotifyStateChanged();
            }
        }
        else if (DragState.SourceSlot != targetSlot)
        {
            // Dropping within inventory - use existing swap
            await Bot.InventoryManager.SwapItems(DragState.SourceSlot!.Value, targetSlot);
            Bot.NotifyStateChanged();
        }

        DragState.Clear();
    }

    public void Dispose()
    {
        Bot.OnStateChanged -= HandleStateChanged;
        DragState.OnDragStateChanged -= HandleStateChanged;
    }
}
