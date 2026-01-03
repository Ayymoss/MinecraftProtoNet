using FluentAssertions;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Tests.Infrastructure;
using MinecraftProtoNet.Packets.Base.Definitions;
using Xunit;
using Xunit.Abstractions;

namespace MinecraftProtoNet.Baritone.Tests.Scenarios;

/// <summary>
/// Integration tests for tool selection and item pickup scenarios.
/// These tests validate the bot's ability to choose correct tools and interact with items.
/// </summary>
public class ToolAndItemScenarioTests(ITestOutputHelper output)
{
    /// <summary>
    /// Test: Bot with pickaxe mines through stone efficiently.
    /// Expected: Mining stone with pickaxe should complete in reasonable time.
    /// </summary>
    [Fact]
    public void ToolChoice_MiningStone_WithPickaxe_CompletesEfficiently()
    {
        // Arrange: Floor with stone wall blocking path, bot has pickaxe
        var world = TestWorldBuilder.Create()
            .WithFloor(63, halfWidth: 20)
            .WithPlayer(0.5, 64, 0.5);

        // Place stone wall at X=2 blocking the path
        world.WithBlock(2, 64, 0, "minecraft:stone")
             .WithBlock(2, 65, 0, "minecraft:stone"); // 2 blocks high

        var runner = new MockedWorldRunner(world);

        // Give bot a diamond pickaxe in hotbar slot 0
        // Reference: registries.json - diamond_pickaxe protocol_id = 938
        runner.Entity.Inventory.SetSlot(36, new Slot 
        { 
            ItemId = 938, // minecraft:diamond_pickaxe
            ItemCount = 1 
        });
        runner.Entity.HeldSlot = 0;

        var goal = new GoalBlock(4, 64, 0); // Goal past the wall

        runner.OnTick += (tick, entity) =>
        {
            if (tick % 50 == 0)
                output.WriteLine($"Tick {tick}: ({entity.Position.X:F1}, {entity.Position.Y:F1}, {entity.Position.Z:F1})");
        };

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 500);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}");
        // With correct tool selection, mining 2 stone blocks should take ~60-100 ticks
        // Without tools (hand), it would take ~300+ ticks
        // If this takes too long, tool selection is broken
        result.Success.Should().BeTrue("bot should be able to mine through stone");
        result.TicksUsed.Should().BeLessThan(300, "with pickaxe, mining should be efficient");
    }

    /// <summary>
    /// Test: Bot with shovel mines through dirt efficiently.
    /// </summary>
    [Fact]
    public void ToolChoice_MiningDirt_WithShovel_CompletesEfficiently()
    {
        // Arrange: Floor with dirt mound blocking path
        var world = TestWorldBuilder.Create()
            .WithFloor(63, halfWidth: 20)
            .WithPlayer(0.5, 64, 0.5);

        // Place dirt wall
        world.WithBlock(2, 64, 0, "minecraft:dirt")
             .WithBlock(2, 65, 0, "minecraft:dirt");

        var runner = new MockedWorldRunner(world);

        // Give bot a diamond shovel
        // Reference: registries.json - diamond_shovel protocol_id = 937
        runner.Entity.Inventory.SetSlot(36, new Slot 
        { 
            ItemId = 937, // minecraft:diamond_shovel
            ItemCount = 1 
        });
        runner.Entity.HeldSlot = 0;

        var goal = new GoalBlock(4, 64, 0);

        runner.OnTick += (tick, entity) =>
        {
            if (tick % 50 == 0)
                output.WriteLine($"Tick {tick}: ({entity.Position.X:F1}, {entity.Position.Y:F1}, {entity.Position.Z:F1})");
        };

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 400);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}");
        result.Success.Should().BeTrue("bot should be able to mine through dirt");
        result.TicksUsed.Should().BeLessThan(250, "with shovel, mining dirt should be fast");
    }

    /// <summary>
    /// Test: Bot with axe mines through logs efficiently.
    /// </summary>
    [Fact]
    public void ToolChoice_MiningWood_WithAxe_CompletesEfficiently()
    {
        // Arrange: Floor with log blocking path
        var world = TestWorldBuilder.Create()
            .WithFloor(63, halfWidth: 20)
            .WithPlayer(0.5, 64, 0.5);

        // Place oak log wall
        world.WithBlock(2, 64, 0, "minecraft:oak_log")
             .WithBlock(2, 65, 0, "minecraft:oak_log");

        var runner = new MockedWorldRunner(world);

        // Give bot a diamond axe
        // Reference: registries.json - diamond_axe protocol_id = 939
        runner.Entity.Inventory.SetSlot(36, new Slot 
        { 
            ItemId = 939, // minecraft:diamond_axe
            ItemCount = 1 
        });
        runner.Entity.HeldSlot = 0;

        var goal = new GoalBlock(4, 64, 0);

        runner.OnTick += (tick, entity) =>
        {
            if (tick % 50 == 0)
                output.WriteLine($"Tick {tick}: ({entity.Position.X:F1}, {entity.Position.Y:F1}, {entity.Position.Z:F1})");
        };

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 400);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}");
        result.Success.Should().BeTrue("bot should be able to mine through logs");
        result.TicksUsed.Should().BeLessThan(250, "with axe, mining logs should be efficient");
    }

    /// <summary>
    /// Test: Combined tool choice - bot must select correct tool for each block type.
    /// Stone -> Pickaxe, Dirt -> Shovel, Log -> Axe
    /// </summary>
    [Fact]
    public void ToolChoice_MixedBlocks_SelectsCorrectTools()
    {
        // Arrange: Row of different block types requiring different tools
        var world = TestWorldBuilder.Create()
            .WithFloor(63, halfWidth: 20)
            .WithPlayer(0.5, 64, 0.5);

        // Blocking walls: Stone at X=2, Dirt at X=4, Log at X=6
        world.WithBlock(2, 64, 0, "minecraft:stone")
             .WithBlock(2, 65, 0, "minecraft:stone")
             .WithBlock(4, 64, 0, "minecraft:dirt")
             .WithBlock(4, 65, 0, "minecraft:dirt")
             .WithBlock(6, 64, 0, "minecraft:oak_log")
             .WithBlock(6, 65, 0, "minecraft:oak_log");

        var runner = new MockedWorldRunner(world);

        // Give bot all three tools in hotbar
        // Reference: registries.json protocol IDs
        runner.Entity.Inventory.SetSlot(36, new Slot { ItemId = 938, ItemCount = 1 }); // diamond_pickaxe
        runner.Entity.Inventory.SetSlot(37, new Slot { ItemId = 937, ItemCount = 1 }); // diamond_shovel
        runner.Entity.Inventory.SetSlot(38, new Slot { ItemId = 939, ItemCount = 1 }); // diamond_axe
        runner.Entity.HeldSlot = 0;

        var goal = new GoalBlock(8, 64, 0); // Past all walls

        runner.OnTick += (tick, entity) =>
        {
            if (tick % 50 == 0)
                output.WriteLine($"Tick {tick}: ({entity.Position.X:F1}, {entity.Position.Y:F1}, {entity.Position.Z:F1}) " +
                                 $"HeldSlot={entity.HeldSlot}");
        };

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 800);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}");
        result.Success.Should().BeTrue("bot should mine through all block types");
        // With correct tool selection: ~180 ticks total
        // Without tools: ~600+ ticks
        result.TicksUsed.Should().BeLessThan(500, "correct tool choices should significantly reduce mining time");
    }

    // ================================================
    // ITEM PICKUP TESTS
    // ================================================

    /// <summary>
    /// Test: Bot picks up a diamond item on the ground while walking to goal.
    /// Reference: ItemEntity pickup radius is ~1.0 blocks
    /// </summary>
    [Fact]
    public void ItemPickup_DiamondOnPath_PicksUpItem()
    {
        // Arrange: Flat floor, spawn diamond at (2, 64, 0), goal at (5, 64, 0)
        var world = TestWorldBuilder.Create()
            .WithFloor(63, halfWidth: 20)
            .WithPlayer(0.5, 64, 0.5);

        var runner = new MockedWorldRunner(world);

        // Spawn a diamond item entity at (2, 64, 0) - on the path to the goal
        // Reference: registries.json - diamond protocol_id = 898
        var diamondItem = runner.ItemEntities.SpawnItem(2.5, 64, 0.5, 898, count: 1);

        var itemsPickedUp = new List<TestItemEntity>();
        runner.OnItemPickup += item => itemsPickedUp.Add(item);

        runner.OnTick += (tick, entity) =>
        {
            if (tick % 20 == 0)
                output.WriteLine($"Tick {tick}: ({entity.Position.X:F1}, {entity.Position.Y:F1}, {entity.Position.Z:F1}) " +
                                 $"Items picked: {itemsPickedUp.Count}");
        };

        var goal = new GoalBlock(5, 64, 0);

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 300);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}, Items picked: {itemsPickedUp.Count}");
        result.Success.Should().BeTrue("bot should reach goal");
        itemsPickedUp.Should().ContainSingle("bot should pick up the diamond");
        itemsPickedUp[0].Item.ItemId.Should().Be(898, "picked up item should be diamond");
    }

    /// <summary>
    /// Test: Bot picks up multiple items on the path.
    /// </summary>
    [Fact]
    public void ItemPickup_MultipleItems_PicksUpAllItems()
    {
        // Arrange: Flat floor, spawn multiple items along the path
        var world = TestWorldBuilder.Create()
            .WithFloor(63, halfWidth: 20)
            .WithPlayer(0.5, 64, 0.5);

        var runner = new MockedWorldRunner(world);

        // Spawn diamonds at intervals along the path
        runner.ItemEntities.SpawnItem(2.5, 64, 0.5, 898, count: 1);  // Diamond at X=2
        runner.ItemEntities.SpawnItem(4.5, 64, 0.5, 898, count: 3);  // 3 Diamonds at X=4
        runner.ItemEntities.SpawnItem(6.5, 64, 0.5, 898, count: 1);  // Diamond at X=6

        var itemsPickedUp = new List<TestItemEntity>();
        runner.OnItemPickup += item => itemsPickedUp.Add(item);

        runner.OnTick += (tick, entity) =>
        {
            if (tick % 30 == 0)
                output.WriteLine($"Tick {tick}: ({entity.Position.X:F1}, {entity.Position.Y:F1}, {entity.Position.Z:F1}) " +
                                 $"Items: {itemsPickedUp.Count}/3");
        };

        var goal = new GoalBlock(8, 64, 0);

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 400);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}, Items picked: {itemsPickedUp.Count}");
        result.Success.Should().BeTrue("bot should reach goal");
        itemsPickedUp.Should().HaveCount(3, "bot should pick up all 3 item entities");
    }

    /// <summary>
    /// Test: Pickup delay is respected - items cannot be picked up until delay expires.
    /// Reference: ItemEntity.pickupDelay in Minecraft
    /// </summary>
    [Fact]
    public void ItemPickup_WithDelay_RespectsPickupDelay()
    {
        // Arrange: Spawn item right at player position with pickup delay
        var world = TestWorldBuilder.Create()
            .WithFloor(63, halfWidth: 20)
            .WithPlayer(0.5, 64, 0.5);

        var runner = new MockedWorldRunner(world);

        // Spawn diamond exactly at player position with 20 tick delay
        var diamondItem = runner.ItemEntities.SpawnItem(0.5, 64, 0.5, 898, count: 1, pickupDelay: 20);

        var pickupTick = -1;
        runner.OnItemPickup += item =>
        {
            pickupTick = runner.TicksElapsed;
            output.WriteLine($"Item picked up at tick {pickupTick}");
        };

        // Manually tick without pathfinding to test pure pickup delay
        for (int i = 0; i < 30; i++)
        {
            runner.Tick();
            output.WriteLine($"Tick {runner.TicksElapsed}: ItemDelay={diamondItem.PickupDelay}, PickedUp={diamondItem.PickedUp}");
        }

        // Assert - pickup happens on the tick where delay BECOMES 0 (transitions from 1 to 0)
        // With 20 tick delay: ticks 1-19 decrement delay from 20 to 1, tick 20 decrements to 0 and pickup happens
        // But since Tick() decrements first then checks, pickup triggers at the tick where delay hits 0
        output.WriteLine($"Final pickup tick: {pickupTick}");
        pickupTick.Should().BeGreaterThanOrEqualTo(19, "item should not be picked up before delay expires");
        pickupTick.Should().BeLessThanOrEqualTo(21, "item should be picked up shortly after delay expires");
    }

    /// <summary>
    /// Test: Bot inventory is correctly updated after picking up items.
    /// </summary>
    [Fact]
    public void ItemPickup_VerifyInventory_ItemAddedToInventory()
    {
        // Arrange
        var world = TestWorldBuilder.Create()
            .WithFloor(63, halfWidth: 20)
            .WithPlayer(0.5, 64, 0.5);

        var runner = new MockedWorldRunner(world);

        // Spawn diamond right next to player (immediate pickup)
        runner.ItemEntities.SpawnItem(1.0, 64, 0.5, 898, count: 5);

        // Check inventory is empty initially
        var initialInventory = runner.Entity.Inventory.Items.Values
            .Where(s => s.ItemId == 898)
            .Sum(s => s.ItemCount);
        initialInventory.Should().Be(0, "inventory should start empty of diamonds");

        var goal = new GoalBlock(3, 64, 0);

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 200);

        // Assert - Check inventory now contains the diamond
        var finalInventory = runner.Entity.Inventory.Items.Values
            .Where(s => s.ItemId == 898)
            .Sum(s => s.ItemCount);

        output.WriteLine($"Result: {result.Message}, Diamonds in inventory: {finalInventory}");
        result.Success.Should().BeTrue("bot should reach goal");
        finalInventory.Should().Be(5, "inventory should contain 5 diamonds after pickup");
    }
}

