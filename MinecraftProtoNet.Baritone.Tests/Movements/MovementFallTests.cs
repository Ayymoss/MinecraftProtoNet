using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Pathfinding;
using MinecraftProtoNet.State;
using Moq;
using Xunit;

namespace MinecraftProtoNet.Baritone.Tests.Movements;

/// <summary>
/// Tests for MovementFall (falling down multiple blocks).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementFall.java
/// </summary>
public class MovementFallTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;

    public MovementFallTests()
    {
        _tickManagerMock = new Mock<ITickManager>();
        _playerRegistryMock = new Mock<IPlayerRegistry>();
        _chunkManagerMock = new Mock<IChunkManager>();
        
        _level = new Level(_tickManagerMock.Object, _playerRegistryMock.Object, _chunkManagerMock.Object);
        _context = new CalculationContext(_level)
        {
            AllowBreak = true,
            AllowPlace = true,
            MaxFallHeightNoWater = 3,
            MaxFallHeightBucket = 20,
            HasWaterBucket = false
        };
    }

    private void SetupBlock(int x, int y, int z, string name, bool hasCollision = true)
    {
        var block = new BlockState(1, name, new Dictionary<string, string>())
        {
            HasCollision = hasCollision
        };
        _chunkManagerMock.Setup(m => m.GetBlockAt(x, y, z)).Returns(block);
    }

    private void SetupAir(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:air", false);
    private void SetupStone(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:stone", true);
    private void SetupWater(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:water", false);

    /// <summary>
    /// Test: 1-3 block fall (safe height) has finite cost.
    /// </summary>
    [Fact]
    public void Fall_Cost_SafeHeight_HasFiniteCost()
    {
        // Arrange: 3-block fall (0,67,0) -> (0,64,1)
        SetupAir(0, 67, 0);
        SetupAir(0, 68, 0);
        SetupStone(0, 66, 0);
        
        // Air column
        for (int y = 64; y <= 68; y++)
            SetupAir(0, y, 1);
        SetupStone(0, 63, 1);   // landing floor
        
        var movement = new MovementFall(0, 67, 0, 0, 64, 1, MoveDirection.DescendSouth);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert
        Assert.False(double.IsPositiveInfinity(cost), "3-block fall should be safe");
        Assert.True(cost > 0, "Cost should be positive");
    }

    /// <summary>
    /// Test: Water bucket MLG reduces effective fall height.
    /// </summary>
    [Fact]
    public void Fall_Cost_WaterBucket_AllowsHighFall()
    {
        // Arrange: 10-block fall with water bucket
        var contextWithBucket = new CalculationContext(_level)
        {
            MaxFallHeightNoWater = 3,
            MaxFallHeightBucket = 20,
            HasWaterBucket = true
        };
        
        SetupAir(0, 74, 0);
        SetupAir(0, 75, 0);
        SetupStone(0, 73, 0);
        
        // 10-block air column
        for (int y = 64; y <= 75; y++)
            SetupAir(0, y, 1);
        SetupStone(0, 63, 1);
        
        var movement = new MovementFall(0, 74, 0, 0, 64, 1, MoveDirection.DescendSouth);

        // Act
        var costNoBucket = movement.CalculateCost(_context);
        var costWithBucket = movement.CalculateCost(contextWithBucket);

        // Assert
        Assert.Equal(ActionCosts.CostInf, costNoBucket); // Too high without bucket
        Assert.False(double.IsPositiveInfinity(costWithBucket), "Should be possible with bucket");
    }

    /// <summary>
    /// Test: Fatal fall height (no water) returns CostInf.
    /// </summary>
    [Fact]
    public void Fall_Cost_FatalHeight_ReturnsInfinity()
    {
        // Arrange: 10-block fall without water bucket or landing water
        SetupAir(0, 74, 0);
        SetupAir(0, 75, 0);
        SetupStone(0, 73, 0);
        
        for (int y = 64; y <= 75; y++)
            SetupAir(0, y, 1);
        SetupStone(0, 63, 1);
        
        var movement = new MovementFall(0, 74, 0, 0, 64, 1, MoveDirection.DescendSouth);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert
        Assert.Equal(ActionCosts.CostInf, cost);
    }
}
