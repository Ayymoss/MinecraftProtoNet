using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Actions;

/// <summary>
/// Manages player interactions with the world (digging, placing, interacting).
/// </summary>
public interface IInteractionManager
{
    /// <summary>
    /// Gets or sets the reach distance for interactions.
    /// Default is usually 4.5 or 5.0.
    /// </summary>
    double ReachDistance { get; set; }

    /// <summary>
    /// Attempts to dig (break) the block the player is currently looking at.
    /// </summary>
    Task<bool> DigBlockAsync();

    /// <summary>
    /// Attempts to place the held item as a block at the position the player is currently looking at.
    /// </summary>
    Task<bool> PlaceBlockAsync(Hand hand = Hand.MainHand);

    /// <summary>
    /// Attempts to place the held item as a block at the specified coordinates.
    /// Makes the entity look at the target position before placing.
    /// </summary>
    Task<bool> PlaceBlockAtAsync(int x, int y, int z, Hand hand = Hand.MainHand);

    /// <summary>
    /// Attempts to interact (right-click) with the entity or block the player is looking at.
    /// </summary>
    Task<bool> InteractAsync(Hand hand = Hand.MainHand);

    /// <summary>
    /// Attacks the entity the player is currently looking at.
    /// </summary>
    Task<bool> AttackAsync();

    /// <summary>
    /// Attacks the specified entity.
    /// </summary>
    Task AttackEntityAsync(Entity target);
    
    /// <summary>
    /// Swings the specified hand.
    /// </summary>
    Task SwingHandAsync(Hand hand);
    
    /// <summary>
    /// Drops the currently held item.
    /// </summary>
    Task<bool> DropHeldItemAsync();
    
    /// <summary>
    /// Sets the selected hotbar slot (0-8).
    /// </summary>
    Task<bool> SetHeldSlotAsync(short slot);

    /// <summary>
    /// Starts breaking a block at the specified position.
    /// Equivalent to Java's MultiPlayerGameMode.startDestroyBlock().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerController.java:91-93
    /// </summary>
    Task<bool> StartDestroyBlockAsync(Vector3<int> position, BlockFace face);

    /// <summary>
    /// Continues breaking a block that was already started.
    /// Equivalent to Java's MultiPlayerGameMode.continueDestroyBlock().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerController.java:60-62
    /// </summary>
    Task<bool> ContinueDestroyBlockAsync(Vector3<int> position, BlockFace face);

    /// <summary>
    /// Stops/cancels breaking the current block.
    /// Equivalent to Java's MultiPlayerGameMode.stopDestroyBlock().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerController.java:65-67
    /// </summary>
    Task ResetBlockRemovingAsync();

    /// <summary>
    /// Checks if a block has been broken (i.e., not currently breaking).
    /// Equivalent to Java's IPlayerController.hasBrokenBlock().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerController.java:55-57
    /// </summary>
    bool HasBrokenBlock();

    /// <summary>
    /// Handles an inventory window click.
    /// Equivalent to Java's IPlayerController.windowClick().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerController.java:70-72
    /// Used by Baritone for inventory management.
    /// </summary>
    /// <param name="windowId">The window/container ID (0 for player inventory).</param>
    /// <param name="slotId">The slot index to click.</param>
    /// <param name="mouseButton">The mouse button (0=left, 1=right).</param>
    /// <param name="clickType">The type of click action.</param>
    Task WindowClickAsync(int windowId, int slotId, int mouseButton, ClickType clickType);

    /// <summary>
    /// Synchronizes the currently held item with the server.
    /// Equivalent to Java's IPlayerController.syncHeldItem().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerController.java:50-52
    /// Used by Baritone to ensure the server knows what item is currently held after block breaking.
    /// </summary>
    Task SyncHeldItemAsync();

    /// <summary>
    /// Sets the hitting block state (whether we're currently breaking a block).
    /// Equivalent to Java's IPlayerController.setHittingBlock().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerController.java:96-98
    /// Used by Baritone for block breaking state management.
    /// </summary>
    /// <param name="hittingBlock">True if currently hitting/breaking a block, false otherwise.</param>
    void SetHittingBlock(bool hittingBlock);
}
