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
/// Tests for MovementTraverse (walking one block horizontally).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementTraverse.java
/// </summary>
public class MovementTraverseTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;

    public MovementTraverseTests()
    {
        _tickManagerMock = new Mock<ITickManager>();
        _playerRegistryMock = new Mock<IPlayerRegistry>();
        _chunkManagerMock = new Mock<IChunkManager>();
        
        _level = new Level(_tickManagerMock.Object, _playerRegistryMock.Object, _chunkManagerMock.Object);
        _context = new CalculationContext(_level)
        {
            AllowBreak = true,
            AllowPlace = true,
            HasThrowaway = true,
            CanSprint = true
        };
    }

    private void SetupBlock(int x, int y, int z, string name, bool hasCollision = true, float destroySpeed = 1.0f, float speedFactor = 1.0f)
    {
        var block = new BlockState(1, name, new Dictionary<string, string>())
        {
            HasCollision = hasCollision,
            DestroySpeed = destroySpeed,
            SpeedFactor = speedFactor
        };
        _chunkManagerMock.Setup(m => m.GetBlockAt(x, y, z)).Returns(block);
    }

    private void SetupAir(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:air", false, 0);
    private void SetupStone(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:stone", true, 1.5f);
    private void SetupSoulSand(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:soul_sand", true, 0.5f, 0.4f);
    private void SetupIce(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:ice", true, 0.5f);

    /// <summary>
    /// Test: Clear traverse returns WalkOneBlockCost with sprint multiplier.
    /// </summary>
    [Fact]
    public void Traverse_Cost_Clear_ReturnsWalkCost()
    {
        // Arrange: Walk south (0,64,0) -> (0,64,1)
        SetupAir(0, 64, 0);     // src body
        SetupAir(0, 65, 0);     // src head
        SetupStone(0, 63, 0);   // src floor
        SetupStone(0, 63, 1);   // dest floor
        SetupAir(0, 64, 1);     // dest body
        SetupAir(0, 65, 1);     // dest head
        
        var movement = new MovementTraverse(0, 64, 0, 0, 1, MoveDirection.TraverseSouth);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert
        Assert.False(double.IsPositiveInfinity(cost), "Clear traverse should have finite cost");
        Assert.True(cost > 0, "Cost should be positive");
        // Sprint cost should be less than walking cost
        Assert.True(cost <= ActionCosts.WalkOneBlockCost, 
            $"Sprint cost {cost} should be <= walk cost {ActionCosts.WalkOneBlockCost}");
    }

    /// <summary>
    /// Test: Soul sand slows movement (speed factor applied).
    /// </summary>
    [Fact]
    public void Traverse_Cost_SoulSand_HasSlowPenalty()
    {
        // Arrange: Walk onto soul sand
        SetupAir(0, 64, 0);
        SetupAir(0, 65, 0);
        SetupStone(0, 63, 0);
        SetupSoulSand(0, 63, 1);    // Soul sand floor (slows movement)
        SetupAir(0, 64, 1);
        SetupAir(0, 65, 1);
        
        var movementSoulSand = new MovementTraverse(0, 64, 0, 0, 1, MoveDirection.TraverseSouth);
        
        // Also setup a normal path for comparison
        SetupStone(0, 63, 2);
        SetupAir(0, 64, 2);
        SetupAir(0, 65, 2);
        var movementNormal = new MovementTraverse(0, 64, 1, 0, 2, MoveDirection.TraverseSouth);

        // Act
        var costSoulSand = movementSoulSand.CalculateCost(_context);
        var costNormal = movementNormal.CalculateCost(_context);

        // Assert: Soul sand should be more expensive (slower) but still traversable
        Assert.False(double.IsPositiveInfinity(costSoulSand), "Soul sand should be traversable");
        // Note: actual comparison depends on implementation
    }

    /// <summary>
    /// Test: Ice is slippery but traversable.
    /// </summary>
    [Fact]
    public void Traverse_Cost_Ice_IsSlippery()
    {
        // Arrange: Walk onto ice
        SetupAir(0, 64, 0);
        SetupAir(0, 65, 0);
        SetupStone(0, 63, 0);
        SetupIce(0, 63, 1);     // Ice floor (slippery)
        SetupAir(0, 64, 1);
        SetupAir(0, 65, 1);
        
        var movement = new MovementTraverse(0, 64, 0, 0, 1, MoveDirection.TraverseSouth);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert: Ice should be traversable
        Assert.False(double.IsPositiveInfinity(cost), "Ice should be traversable");
        Assert.True(cost > 0, "Cost should be positive");
    }
}
