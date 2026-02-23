using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Manages container/menu interactions (chests, villagers, crafting tables, etc.).
/// Implements client-side prediction matching vanilla Minecraft's AbstractContainerMenu.doClick().
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/inventory/AbstractContainerMenu.java
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/MultiPlayerGameMode.java
/// </summary>
public class ContainerManager : IContainerManager
{
    private readonly IMinecraftClient _client;
    private readonly ClientState _state;
    private readonly ILogger<ContainerManager> _logger;
    private TaskCompletionSource<ContainerState>? _containerOpenWaiter;

    public ContainerManager(IMinecraftClient client, ClientState state, ILogger<ContainerManager> logger)
    {
        _client = client;
        _state = state;
        _logger = logger;

        if (_state.LocalPlayer.HasEntity)
        {
            SubscribeToEntityEvents(_state.LocalPlayer.Entity);
        }
    }

    private void SubscribeToEntityEvents(State.Entity entity)
    {
        entity.OnContainerOpened += HandleContainerOpened;
    }

    private void HandleContainerOpened(ContainerState container)
    {
        _containerOpenWaiter?.TrySetResult(container);
        OnContainerOpened?.Invoke(container);

        container.OnContainerClosed += () =>
        {
            OnContainerClosed?.Invoke();
        };
    }

    public ContainerState? CurrentContainer => _state.LocalPlayer.Entity?.CurrentContainer;
    public bool IsContainerOpen => CurrentContainer?.IsOpen == true;

    public event Action<ContainerState>? OnContainerOpened;
    public event Action? OnContainerClosed;

    public async Task<bool> InteractWithEntityAsync(int entityId, Hand hand = Hand.MainHand)
    {
        if (!_state.LocalPlayer.HasEntity)
        {
            _logger.LogWarning("Cannot interact: no local player entity");
            return false;
        }

        _containerOpenWaiter = new TaskCompletionSource<ContainerState>();

        var interactPacket = new InteractPacket
        {
            EntityId = entityId,
            Type = InteractType.Interact,
            Hand = hand,
            SneakKeyPressed = _state.LocalPlayer.Entity.IsSneaking
        };

        await _client.SendPacketAsync(interactPacket);
        _logger.LogDebug("Sent interact packet for entity {Id}", entityId);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var container = await _containerOpenWaiter.Task.WaitAsync(cts.Token);
            _logger.LogInformation("Container opened: {Type} - \"{Title}\"", container.Type, container.Title);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("No container opened after interact (entity may not have a UI)");
            return false;
        }
        finally
        {
            _containerOpenWaiter = null;
        }
    }

    /// <summary>
    /// Clicks a slot in the current container with full client-side prediction.
    /// Mirrors vanilla's MultiPlayerGameMode.handleContainerInput() flow:
    /// 1. Snapshot all slots before the click
    /// 2. Simulate the click locally (doClick)
    /// 3. Diff to build changedSlots
    /// 4. Send packet with post-click carriedItem
    /// 5. Apply predicted state locally
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/MultiPlayerGameMode.java:432-459
    /// </summary>
    public async Task ClickSlotAsync(short slot, ClickContainerMode mode = ClickContainerMode.Pickup, sbyte button = 0)
    {
        if (!IsContainerOpen)
        {
            _logger.LogWarning("Cannot click slot: no container open");
            return;
        }

        var container = CurrentContainer!;
        var entity = _state.LocalPlayer.Entity!;
        var inventory = entity.Inventory;

        // 1. Snapshot all slots BEFORE the click
        // Reference: MultiPlayerGameMode.java:440-443 — snapshots all slot contents
        var totalSlotCount = GetTotalSlotCount(container);
        var beforeSlots = new Dictionary<short, Slot>(totalSlotCount);
        for (short i = 0; i < totalSlotCount; i++)
        {
            beforeSlots[i] = GetWindowSlot(container, entity, i).Clone();
        }
        var beforeCursor = inventory.CursorItem.Clone();

        // 2. Simulate the click locally
        // Reference: MultiPlayerGameMode.java:445 — containerMenu.clicked(slotNum, buttonNum, containerInput, player)
        var newCursor = SimulateClick(container, entity, slot, mode, button);

        // 3. Diff: build changedSlots with only actually-changed slots
        // Reference: MultiPlayerGameMode.java:447-453 — compares before vs after for each slot
        var changedSlots = new Dictionary<short, Slot>();
        for (short i = 0; i < totalSlotCount; i++)
        {
            var afterSlot = GetWindowSlot(container, entity, i);
            if (!Slot.Matches(beforeSlots[i], afterSlot))
            {
                changedSlots[i] = afterSlot;
            }
        }

        // 4. Build and send the packet
        // Reference: MultiPlayerGameMode.java:455-459
        var clickPacket = new ClickContainerPacket
        {
            WindowId = container.ContainerId,
            StateId = container.StateId,
            Slot = slot,
            Button = button,
            Mode = mode,
            ChangedSlots = changedSlots,
            CarriedItem = newCursor
        };

        await _client.SendPacketAsync(clickPacket);

        // 5. Apply predicted cursor state locally
        inventory.CursorItem = newCursor;

        _logger.LogDebug("Clicked slot {Slot} in container {Id} (mode: {Mode}, changed: {Changed})",
            slot, container.ContainerId, mode, changedSlots.Count);
    }

    /// <summary>
    /// Simulates a click action locally, updating slot contents in place and returning the new cursor item.
    /// This mirrors vanilla's AbstractContainerMenu.doClick() for the supported modes.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/inventory/AbstractContainerMenu.java doClick()
    /// </summary>
    private Slot SimulateClick(ContainerState container, State.Entity entity, short slotIndex,
        ClickContainerMode mode, sbyte button)
    {
        var inventory = entity.Inventory;
        var carried = inventory.CursorItem.Clone();

        switch (mode)
        {
            case ClickContainerMode.Pickup:
                return SimulatePickup(container, entity, slotIndex, button, carried);

            case ClickContainerMode.Swap:
                SimulateSwap(container, entity, slotIndex, button);
                return carried; // Swap doesn't change cursor

            case ClickContainerMode.QuickMove:
                // QuickMove is container-type-specific (quickMoveStack override).
                // We can't generically predict it, so return current cursor unchanged.
                // Server will resync the affected slots.
                // Reference: AbstractContainerMenu.java quickMoveStack() — overridden per container type
                return carried;

            case ClickContainerMode.Throw:
                return SimulateThrow(container, entity, slotIndex, button, carried);

            default:
                // Clone, QuickCraft, PickupAll — not locally predicted, server will resync
                return carried;
        }
    }

    /// <summary>
    /// Simulates Pickup mode (left-click / right-click on a slot).
    /// Reference: AbstractContainerMenu.java doClick() — PICKUP branch
    /// </summary>
    private Slot SimulatePickup(ContainerState container, State.Entity entity, short slotIndex,
        sbyte button, Slot carried)
    {
        bool isLeftClick = button == 0;

        // Click outside window (-999) = drop carried items
        // Reference: AbstractContainerMenu.java — slotIndex == SLOT_CLICKED_OUTSIDE
        if (slotIndex == -999)
        {
            if (!carried.IsEmpty)
            {
                if (isLeftClick)
                {
                    // Drop all
                    return Slot.Empty;
                }
                else
                {
                    // Drop one
                    carried.ItemCount--;
                    return carried.ItemCount <= 0 ? Slot.Empty : carried;
                }
            }
            return carried;
        }

        if (slotIndex < 0) return carried;

        var clickedSlot = GetWindowSlot(container, entity, slotIndex);

        if (clickedSlot.IsEmpty)
        {
            if (!carried.IsEmpty)
            {
                // Place carried item into empty slot
                // Reference: AbstractContainerMenu.java — clicked.isEmpty() && !carried.isEmpty()
                int amount = isLeftClick ? carried.ItemCount : 1;
                var placed = carried.CopyWithCount(Math.Min(amount, carried.GetMaxStackSize()));
                SetWindowSlot(container, entity, slotIndex, placed);

                carried.ItemCount -= placed.ItemCount;
                return carried.ItemCount <= 0 ? Slot.Empty : carried;
            }
        }
        else
        {
            if (carried.IsEmpty)
            {
                // Pick up from slot
                // Reference: AbstractContainerMenu.java — carried.isEmpty()
                int amount = isLeftClick ? clickedSlot.ItemCount : (clickedSlot.ItemCount + 1) / 2;
                var pickedUp = clickedSlot.CopyWithCount(amount);

                clickedSlot.ItemCount -= amount;
                if (clickedSlot.ItemCount <= 0)
                {
                    SetWindowSlot(container, entity, slotIndex, Slot.Empty);
                }
                else
                {
                    SetWindowSlot(container, entity, slotIndex, clickedSlot);
                }

                return pickedUp;
            }
            else if (Slot.IsSameItemSameComponents(clickedSlot, carried))
            {
                // Stack same items together
                // Reference: AbstractContainerMenu.java — isSameItemSameComponents(clicked, carried)
                int maxStackSize = clickedSlot.GetMaxStackSize();
                int amount = isLeftClick ? carried.ItemCount : 1;
                int canFit = maxStackSize - clickedSlot.ItemCount;
                int toPlace = Math.Min(amount, canFit);

                if (toPlace > 0)
                {
                    clickedSlot.ItemCount += toPlace;
                    SetWindowSlot(container, entity, slotIndex, clickedSlot);
                    carried.ItemCount -= toPlace;
                }

                return carried.ItemCount <= 0 ? Slot.Empty : carried;
            }
            else
            {
                // Swap different items
                // Reference: AbstractContainerMenu.java — swap branch
                if (carried.ItemCount <= clickedSlot.GetMaxStackSize())
                {
                    SetWindowSlot(container, entity, slotIndex, carried);
                    return clickedSlot;
                }
                // Can't place oversized stack, return cursor unchanged
                return carried;
            }
        }

        return carried;
    }

    /// <summary>
    /// Simulates Swap mode (number key 1-9 to swap with hotbar slot).
    /// Reference: AbstractContainerMenu.java doClick() — SWAP branch
    /// </summary>
    private void SimulateSwap(ContainerState container, State.Entity entity, short slotIndex, sbyte button)
    {
        var inventory = entity.Inventory;

        // button 0-8 = hotbar slots 0-8, button 40 = offhand
        short hotbarContainerSlot = (short)(button + 36);
        var hotbarItem = inventory.GetSlot(hotbarContainerSlot).Clone();
        var targetItem = GetWindowSlot(container, entity, slotIndex).Clone();

        // Swap the two slots
        // Reference: AbstractContainerMenu.java — SWAP branch, both non-empty case
        inventory.SetSlot(hotbarContainerSlot, targetItem);
        SetWindowSlot(container, entity, slotIndex, hotbarItem);
    }

    /// <summary>
    /// Simulates Throw mode (Q to drop from slot).
    /// Reference: AbstractContainerMenu.java doClick() — THROW branch
    /// </summary>
    private Slot SimulateThrow(ContainerState container, State.Entity entity, short slotIndex,
        sbyte button, Slot carried)
    {
        if (!carried.IsEmpty || slotIndex < 0) return carried;

        var slotItem = GetWindowSlot(container, entity, slotIndex);
        if (slotItem.IsEmpty) return carried;

        int amount = button == 0 ? 1 : slotItem.ItemCount; // Q=1, Ctrl+Q=all
        slotItem.ItemCount -= amount;
        if (slotItem.ItemCount <= 0)
        {
            SetWindowSlot(container, entity, slotIndex, Slot.Empty);
        }
        else
        {
            SetWindowSlot(container, entity, slotIndex, slotItem);
        }

        return carried;
    }

    #region Slot Access Helpers

    /// <summary>
    /// Gets the total number of slots in the container window (container + player inventory).
    /// Container window layout: [container slots 0..N-1] [player main inv 9-35] [hotbar 36-44]
    /// Total = containerSlotCount + 36 (player main 27 + hotbar 9)
    /// </summary>
    private static int GetTotalSlotCount(ContainerState container)
    {
        return container.Type.GetContainerSlotCount() + 36;
    }

    /// <summary>
    /// Gets a slot from the unified container window by window slot index.
    /// Window slots 0..containerSlotCount-1 → container.GetSlot()
    /// Window slots containerSlotCount..end → entity.Inventory.GetSlot() with offset mapping
    /// Formula: playerSlot = (windowSlot - containerSlotCount) + 9
    /// Reference: InventoryHandler.cs slot mapping formula
    /// </summary>
    private static Slot GetWindowSlot(ContainerState container, State.Entity entity, short windowSlot)
    {
        var containerSlotCount = container.Type.GetContainerSlotCount();
        if (windowSlot < containerSlotCount)
        {
            return container.GetSlot(windowSlot);
        }
        else
        {
            short playerSlot = (short)(windowSlot - containerSlotCount + 9);
            return entity.Inventory.GetSlot(playerSlot);
        }
    }

    /// <summary>
    /// Sets a slot in the unified container window by window slot index.
    /// Applies to either the container or the player inventory depending on the slot index.
    /// </summary>
    private static void SetWindowSlot(ContainerState container, State.Entity entity, short windowSlot, Slot slot)
    {
        var containerSlotCount = container.Type.GetContainerSlotCount();
        if (windowSlot < containerSlotCount)
        {
            container.SetSlot(windowSlot, slot);
        }
        else
        {
            short playerSlot = (short)(windowSlot - containerSlotCount + 9);
            entity.Inventory.SetSlot(playerSlot, slot);
        }
    }

    #endregion

    public async Task SelectTradeAsync(int tradeIndex)
    {
        if (!IsContainerOpen || CurrentContainer?.Type != MenuType.Merchant)
        {
            _logger.LogWarning("Cannot select trade: no merchant container open");
            return;
        }

        if (CurrentContainer.MerchantData == null)
        {
            _logger.LogWarning("Cannot select trade: no merchant data available");
            return;
        }

        if (tradeIndex < 0 || tradeIndex >= CurrentContainer.MerchantData.Offers.Count)
        {
            _logger.LogWarning("Invalid trade index: {Index} (available: {Count})",
                tradeIndex, CurrentContainer.MerchantData.Offers.Count);
            return;
        }

        CurrentContainer.MerchantData.SelectedTradeIndex = tradeIndex;

        var selectPacket = new SelectTradePacket
        {
            SelectedSlot = tradeIndex
        };

        await _client.SendPacketAsync(selectPacket);
        _logger.LogDebug("Selected trade {Index} in merchant container", tradeIndex);
    }

    /// <summary>
    /// Quick-moves (shift-click) a slot. QuickMove destination is container-type-specific
    /// and we cannot generically predict it, so we send the packet and let the server
    /// handle the slot changes (server will resync affected slots).
    /// Reference: AbstractContainerMenu.java quickMoveStack() — overridden per container type
    /// </summary>
    public async Task QuickMoveSlotAsync(short slot, int windowId)
    {
        var entity = _state.LocalPlayer.Entity;
        if (entity == null)
        {
            _logger.LogWarning("Cannot quick-move: no local player entity");
            return;
        }

        int stateId;
        if (windowId == 0)
        {
            stateId = entity.Inventory.StateId;
        }
        else if (IsContainerOpen && CurrentContainer?.ContainerId == windowId)
        {
            stateId = CurrentContainer.StateId;
        }
        else
        {
            _logger.LogWarning("Cannot quick-move: invalid window ID {WindowId}", windowId);
            return;
        }

        // CarriedItem should be current cursor (unaffected by QuickMove)
        // Reference: MultiPlayerGameMode.java:455 — carriedItem = getCarried() after clicked()
        // QuickMove doesn't modify the cursor
        var clickPacket = new ClickContainerPacket
        {
            WindowId = windowId,
            StateId = stateId,
            Slot = slot,
            Button = 0,
            Mode = ClickContainerMode.QuickMove,
            ChangedSlots = new Dictionary<short, Slot>(),
            CarriedItem = entity.Inventory.CursorItem
        };

        await _client.SendPacketAsync(clickPacket);
        _logger.LogDebug("Quick-moved slot {Slot} in window {WindowId}", slot, windowId);
    }

    public async Task CloseContainerAsync()
    {
        if (!IsContainerOpen)
        {
            _logger.LogDebug("No container to close");
            return;
        }

        var containerId = CurrentContainer!.ContainerId;

        var closePacket = new CloseContainerPacket
        {
            ContainerId = containerId
        };

        await _client.SendPacketAsync(closePacket);

        CurrentContainer.Close();
        _state.LocalPlayer.Entity!.CurrentContainer = null;

        _logger.LogDebug("Closed container {Id}", containerId);
    }
}
