using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base.Definitions;

namespace MinecraftProtoNet.Baritone.Tests.Infrastructure;

/// <summary>
/// Represents an item entity in the test world that can be picked up.
/// Reference: net.minecraft.world.entity.item.ItemEntity
/// </summary>
public class TestItemEntity
{
    private static int _nextEntityId = 1000;

    /// <summary>
    /// Unique entity ID.
    /// </summary>
    public int EntityId { get; }

    /// <summary>
    /// Current position in the world.
    /// </summary>
    public Vector3<double> Position { get; set; }

    /// <summary>
    /// The item stack this entity represents.
    /// </summary>
    public Slot Item { get; set; }

    /// <summary>
    /// Whether this item has been picked up.
    /// </summary>
    public bool PickedUp { get; private set; }

    /// <summary>
    /// Pickup delay in ticks (items can't be picked up immediately after spawning).
    /// Reference: ItemEntity.pickupDelay - default 10 ticks for player-dropped items
    /// </summary>
    public int PickupDelay { get; set; } = 0;

    /// <summary>
    /// Age in ticks. Items despawn after 6000 ticks (5 minutes).
    /// </summary>
    public int Age { get; private set; }

    /// <summary>
    /// Pickup radius for players (1.0 block radius).
    /// Reference: ItemEntity - pickup distance is ~1.0 blocks from player center
    /// </summary>
    public const double PickupRadius = 1.0;

    public TestItemEntity(Vector3<double> position, Slot item)
    {
        EntityId = _nextEntityId++;
        Position = position;
        Item = item;
    }

    /// <summary>
    /// Creates an item entity from item ID and count.
    /// </summary>
    public static TestItemEntity Create(double x, double y, double z, int itemId, int count = 1)
    {
        return new TestItemEntity(
            new Vector3<double>(x, y, z),
            new Slot { ItemId = itemId, ItemCount = (sbyte)count }
        );
    }

    /// <summary>
    /// Ticks this item entity (age, despawn check).
    /// </summary>
    public void Tick()
    {
        Age++;
        if (PickupDelay > 0) PickupDelay--;
    }

    /// <summary>
    /// Marks this item as picked up.
    /// </summary>
    public void MarkPickedUp()
    {
        PickedUp = true;
    }

    /// <summary>
    /// Checks if this item can be picked up by a player at the given position.
    /// </summary>
    public bool CanBePickedUpBy(Vector3<double> playerPosition)
    {
        if (PickedUp || PickupDelay > 0) return false;

        // Check distance (player feet position to item position)
        var dx = Position.X - playerPosition.X;
        var dy = Position.Y - playerPosition.Y;
        var dz = Position.Z - playerPosition.Z;

        // Distance check (squared for performance)
        var distSq = dx * dx + dy * dy + dz * dz;
        return distSq <= PickupRadius * PickupRadius;
    }
}

/// <summary>
/// Manages item entities in the test world.
/// </summary>
public class TestItemEntityManager
{
    private readonly List<TestItemEntity> _items = new();

    /// <summary>
    /// All item entities in the world.
    /// </summary>
    public IReadOnlyList<TestItemEntity> Items => _items;

    /// <summary>
    /// Event fired when an item is picked up.
    /// </summary>
    public event Action<TestItemEntity>? OnItemPickedUp;

    /// <summary>
    /// Spawns an item entity in the world.
    /// </summary>
    public TestItemEntity SpawnItem(double x, double y, double z, int itemId, int count = 1, int pickupDelay = 0)
    {
        var item = TestItemEntity.Create(x, y, z, itemId, count);
        item.PickupDelay = pickupDelay;
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// Ticks all item entities and checks for pickup.
    /// </summary>
    public List<TestItemEntity> TickAndCheckPickup(Vector3<double> playerPosition)
    {
        var pickedUp = new List<TestItemEntity>();

        foreach (var item in _items.ToList()) // Copy to allow modification
        {
            if (item.PickedUp) continue;

            item.Tick();

            if (item.CanBePickedUpBy(playerPosition))
            {
                item.MarkPickedUp();
                pickedUp.Add(item);
                OnItemPickedUp?.Invoke(item);
            }
        }

        // Remove picked up items
        _items.RemoveAll(i => i.PickedUp);

        return pickedUp;
    }

    /// <summary>
    /// Gets all items within pickup range of a position.
    /// </summary>
    public IEnumerable<TestItemEntity> GetItemsInRange(Vector3<double> position)
    {
        return _items.Where(i => i.CanBePickedUpBy(position));
    }

    /// <summary>
    /// Clears all items.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
    }
}
