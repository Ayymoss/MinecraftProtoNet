using Microsoft.AspNetCore.Components.Web;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.Utilities;

namespace Bot.Webcore.Components.Pages.Components;

public partial class ContainerView
{
    private ContainerState? Container => Bot.State.LocalPlayer?.Entity?.CurrentContainer;

    protected override void OnInitialized()
    {
        Bot.OnStateChanged += HandleStateChanged;
        DragState.OnDragStateChanged += HandleStateChanged;

        // Also subscribe to container events if entity exists
        if (Bot.State.LocalPlayer?.Entity != null)
        {
            Bot.State.LocalPlayer.Entity.OnContainerOpened += HandleContainerOpened;
        }
    }

    private void HandleStateChanged() => InvokeAsync(StateHasChanged);

    private void HandleContainerOpened(ContainerState container)
    {
        container.OnContainerChanged += HandleStateChanged;
        container.OnContainerClosed += HandleStateChanged;
        InvokeAsync(StateHasChanged);
    }

    private int GetColumnCount() => Container?.Type.GetColumnCount() ?? 9;
    private int GetSlotCount() => Container?.Type.GetContainerSlotCount() ?? 0;

    private string GetContainerIcon() => Container?.Type switch
    {
        MenuType.Generic9x1 or MenuType.Generic9x2 or MenuType.Generic9x3
            or MenuType.Generic9x4 or MenuType.Generic9x5 or MenuType.Generic9x6 => "ph-package",
        MenuType.Crafting or MenuType.Crafter3x3 => "ph-wrench",
        MenuType.Furnace or MenuType.BlastFurnace or MenuType.Smoker => "ph-fire",
        MenuType.Merchant => "ph-storefront",
        MenuType.Enchantment => "ph-magic-wand",
        MenuType.Anvil or MenuType.Smithing or MenuType.Grindstone => "ph-hammer",
        MenuType.BrewingStand => "ph-flask",
        MenuType.Hopper => "ph-funnel",
        _ => "ph-cube"
    };

    private string GetSlotClass(bool isDragging = false)
    {
        var baseClass =
            "w-full aspect-square rounded-md flex flex-col items-center justify-center p-0.5 transition-all duration-150 overflow-hidden min-h-[48px] ";

        if (isDragging)
            return baseClass + "bg-blue-900/50 border-2 border-blue-400 opacity-50";

        return baseClass + "bg-slate-800/80 border border-slate-600/50 hover:border-amber-500 hover:bg-slate-700/80";
    }

    private string GetItemDisplayName(Slot slot)
    {
        // Try custom name first, then registry lookup
        var customName = ItemTextHelper.GetDisplayName(slot);
        if (!string.IsNullOrEmpty(customName)) return customName;

        var name = Bot.ItemRegistry.GetItemName(slot.ItemId ?? 0);
        if (string.IsNullOrEmpty(name)) return $"#{slot.ItemId}";
        return name.Replace("minecraft:", "").Replace("_", " ");
    }

    private string GetSlotTooltip(Slot slot)
    {
        if (!slot.ItemId.HasValue || slot.ItemCount <= 0) return "Empty";

        var lines = new List<string> { GetItemDisplayName(slot) };

        // Add lore
        var lore = ItemTextHelper.GetLore(slot);
        lines.AddRange(lore);

        return string.Join("\n", lines);
    }

    private async Task OnSlotClick(short slotIndex, MouseEventArgs e)
    {
        if (Bot.ContainerManager is not null)
        {
            if (e.ShiftKey)
            {
                // Shift-click: Quick Move to Inventory
                await Bot.ContainerManager.QuickMoveSlotAsync(slotIndex, Container!.ContainerId);
            }
            else
            {
                // Normal click: Pickup/Place
                await Bot.ContainerManager.ClickSlotAsync(slotIndex);
            }
        }
    }

    private async Task SelectTrade(int tradeIndex)
    {
        if (Bot.ContainerManager is not null)
        {
            await Bot.ContainerManager.SelectTradeAsync(tradeIndex);
        }
    }

    private async Task CloseContainer()
    {
        if (Bot.ContainerManager is not null)
        {
            await Bot.ContainerManager.CloseContainerAsync();
        }
    }

    private void OnDragStart(short slotIndex, Slot slot)
    {
        DragState.StartDrag(slotIndex, fromContainer: true, slot);
    }

    private async Task OnDrop(short targetSlot)
    {
        if (!DragState.IsDragging || Bot.ContainerManager is null) return;

        // We are dropping onto a Container Slot

        if (DragState.IsFromContainer)
        {
            // Dragged from Container -> Container
            // Click Source (Pickup) -> Click Target (Place)
            await Bot.ContainerManager.ClickSlotAsync((short)DragState.SourceSlot!.Value);
            await Bot.ContainerManager.ClickSlotAsync(targetSlot);
        }
        else
        {
            // Dragged from Inventory -> Container
            var containerSlotCount = GetSlotCount();

            // Map Inventory Slot (9..44) to Correct Window Slot Index
            // Formula: ContainerSlotCount + (InventorySlot - 9)
            short mappedSourceSlot = (short)(containerSlotCount + DragState.SourceSlot!.Value - 9);

            await Bot.ContainerManager.ClickSlotAsync(mappedSourceSlot);
            await Bot.ContainerManager.ClickSlotAsync(targetSlot);
        }

        DragState.Clear();
    }

    public void Dispose()
    {
        Bot.OnStateChanged -= HandleStateChanged;
        DragState.OnDragStateChanged -= HandleStateChanged;

        if (Bot.State.LocalPlayer?.Entity != null)
        {
            Bot.State.LocalPlayer.Entity.OnContainerOpened -= HandleContainerOpened;
        }
    }
}
