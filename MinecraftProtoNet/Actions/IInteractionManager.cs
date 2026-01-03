using MinecraftProtoNet.Enums;
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
}
