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
/// Tests for MovementDescend (stepping down one block).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDescend.java
/// </summary>
public class MovementDescendTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;

    public MovementDescendTests()
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
            JumpPenalty = 2.0
        };
    }

    private void SetupBlock(int x, int y, int z, string name, bool hasCollision = true, float destroySpeed = 1.0f)
    {
        var block = new BlockState(1, name, new Dictionary<string, string>())
        {
            HasCollision = hasCollision,
            DestroySpeed = destroySpeed
        };
        _chunkManagerMock.Setup(m => m.GetBlockAt(x, y, z)).Returns(block);
    }

    private void SetupAir(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:air", false, 0);
    private void SetupStone(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:stone", true, 1.5f);

    /// <summary>
    /// Test: Clear descend returns base cost.
    /// </summary>
    [Fact]
    public void Descend_Cost_Clear_ReturnsBaseCost()
    {
        // Arrange: Step down south (0,65,0) -> (0,64,1)
        SetupAir(0, 65, 0);     // src body
        SetupAir(0, 66, 0);     // src head
        SetupStone(0, 64, 0);   // src floor
        SetupStone(0, 63, 1);   // dest floor
        SetupAir(0, 64, 1);     // dest body
        SetupAir(0, 65, 1);     // dest head
        SetupAir(0, 66, 1);     // forward head clearance
        
        var movement = new MovementDescend(0, 65, 0, 0, 1, MoveDirection.DescendSouth);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert
        Assert.False(double.IsPositiveInfinity(cost), "Clear descend should have finite cost");
        Assert.True(cost > 0, "Cost should be positive");
    }

    /// <summary>
    /// Test: Missing floor at destination needs placement.
    /// </summary>
    [Fact]
    public void Descend_Cost_NeedPlace_ReturnsBlocksToPlace()
    {
        // Arrange: No floor at destination
        SetupAir(0, 65, 0);
        SetupAir(0, 66, 0);
        SetupStone(0, 64, 0);
        SetupAir(0, 63, 1);     // dest floor - AIR (hole!)
        SetupAir(0, 64, 1);
        SetupAir(0, 65, 1);
        SetupAir(0, 66, 1);
        
        var movement = new MovementDescend(0, 65, 0, 0, 1, MoveDirection.DescendSouth);

        // Act
        var blocksToPlace = movement.GetBlocksToPlace(_context).ToList();

        // Assert
        Assert.Contains((0, 63, 1), blocksToPlace);
    }

    /// <summary>
    /// Test: Forward head clearance blocked is detected.
    /// </summary>
    [Fact]
    public void Descend_GetBlocksToBreak_ForwardHeadBlocked()
    {
        // Arrange: Stone at forward head position
        SetupAir(0, 65, 0);
        SetupAir(0, 66, 0);
        SetupStone(0, 64, 0);
        SetupStone(0, 63, 1);
        SetupAir(0, 64, 1);
        SetupAir(0, 65, 1);
        SetupStone(0, 66, 1);   // BLOCKED - forward head clearance
        
        var movement = new MovementDescend(0, 65, 0, 0, 1, MoveDirection.DescendSouth);

        // Act
        var blocksToBreak = movement.GetBlocksToBreak(_context).ToList();

        // Assert
        Assert.Contains((0, 66, 1), blocksToBreak);
    }
}
