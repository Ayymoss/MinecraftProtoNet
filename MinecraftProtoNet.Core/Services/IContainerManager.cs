using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Interface for container/menu interaction operations.
/// </summary>
public interface IContainerManager
{
    /// <summary>
    /// The currently open container, or null if none.
    /// </summary>
    ContainerState? CurrentContainer { get; }
    
    /// <summary>
    /// Whether a container is currently open.
    /// </summary>
    bool IsContainerOpen { get; }

    /// <summary>
    /// Interacts with an entity (right-click) to potentially open a container.
    /// </summary>
    /// <param name="entityId">The entity ID to interact with.</param>
    /// <param name="hand">Which hand to use for interaction.</param>
    /// <returns>True if a container was opened within timeout, false otherwise.</returns>
    Task<bool> InteractWithEntityAsync(int entityId, Hand hand = Hand.MainHand);

    /// <summary>
    /// Clicks a slot in the current container.
    /// </summary>
    /// <param name="slot">Slot index to click.</param>
    /// <param name="mode">Click mode (pickup, quick move, etc.).</param>
    /// <param name="button">Mouse button (0=left, 1=right).</param>
    Task ClickSlotAsync(short slot, ClickContainerMode mode = ClickContainerMode.Pickup, sbyte button = 0);

    /// <summary>
    /// Selects a trade in a merchant container.
    /// </summary>
    /// <param name="tradeIndex">Index of the trade to select.</param>
    Task SelectTradeAsync(int tradeIndex);

    /// <summary>
    /// Quick-moves (shift-click) a slot's contents to the other container.
    /// If from container slot, moves to player inventory. If from inventory, moves to container.
    /// </summary>
    /// <param name="slot">Slot index to quick-move.</param>
    /// <param name="windowId">Window ID (0 for inventory, container ID for remote).</param>
    Task QuickMoveSlotAsync(short slot, int windowId);

    /// <summary>
    /// Closes the currently open container.
    /// </summary>
    Task CloseContainerAsync();

    /// <summary>
    /// Event fired when a container is opened.
    /// </summary>
    event Action<ContainerState>? OnContainerOpened;
    
    /// <summary>
    /// Event fired when a container is closed.
    /// </summary>
    event Action? OnContainerClosed;
}
