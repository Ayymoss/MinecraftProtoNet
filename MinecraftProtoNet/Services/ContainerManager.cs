using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.State;
using MinecraftProtoNet.State.Base;

namespace MinecraftProtoNet.Services;

/// <summary>
/// Manages container/menu interactions (chests, villagers, crafting tables, etc.).
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

        // Subscribe to container events from the entity
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
        // Complete any pending wait and fire our own event
        _containerOpenWaiter?.TrySetResult(container);
        OnContainerOpened?.Invoke(container);

        // Subscribe to container close
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

        // Set up waiter for container open
        _containerOpenWaiter = new TaskCompletionSource<ContainerState>();

        // Send interact packet
        var interactPacket = new InteractPacket
        {
            EntityId = entityId,
            Type = InteractType.Interact,
            Hand = hand,
            SneakKeyPressed = _state.LocalPlayer.Entity.IsSneaking
        };

        await _client.SendPacketAsync(interactPacket);
        _logger.LogDebug("Sent interact packet for entity {Id}", entityId);

        // Wait for container to open (with timeout)
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

    public async Task ClickSlotAsync(short slot, ClickContainerMode mode = ClickContainerMode.Pickup, sbyte button = 0)
    {
        if (!IsContainerOpen)
        {
            _logger.LogWarning("Cannot click slot: no container open");
            return;
        }

        var container = CurrentContainer!;
        var entity = _state.LocalPlayer.Entity!;

        // Build click packet
        var clickPacket = new ClickContainerPacket
        {
            WindowId = container.ContainerId,
            StateId = container.StateId,
            Slot = slot,
            Button = button,
            Mode = mode,
            ChangedSlots = new Dictionary<short, Slot>(),
            CarriedItem = entity.Inventory.CursorItem
        };

        // For basic pickup mode, predict the changed slots
        if (mode == ClickContainerMode.Pickup && slot >= 0)
        {
            var clickedSlot = container.GetSlot(slot);
            clickPacket.ChangedSlots[slot] = entity.Inventory.CursorItem;
            // Note: Full state prediction is complex; server will correct if wrong
        }

        await _client.SendPacketAsync(clickPacket);
        _logger.LogDebug("Clicked slot {Slot} in container {Id} (mode: {Mode})", slot, container.ContainerId, mode);
    }

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

    public async Task QuickMoveSlotAsync(short slot, int windowId)
    {
        var entity = _state.LocalPlayer.Entity;
        if (entity == null)
        {
            _logger.LogWarning("Cannot quick-move: no local player entity");
            return;
        }

        // Determine state ID based on window
        int stateId;
        if (windowId == 0)
        {
            // Player inventory window - use inventory state
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

        // Build click packet with QuickMove mode (shift-click)
        var clickPacket = new ClickContainerPacket
        {
            WindowId = windowId,
            StateId = stateId,
            Slot = slot,
            Button = 0, // Left click
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
        
        // Clear local state immediately
        CurrentContainer.Close();
        _state.LocalPlayer.Entity!.CurrentContainer = null;
        
        _logger.LogDebug("Closed container {Id}", containerId);
    }
}
