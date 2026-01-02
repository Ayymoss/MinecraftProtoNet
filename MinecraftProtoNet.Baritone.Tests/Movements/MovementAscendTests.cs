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
/// Tests for MovementAscend (jumping up one block while moving forward).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementAscend.java
/// </summary>
public class MovementAscendTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;

    public MovementAscendTests()
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
    /// Test: Clear path ascend returns base cost.
    /// </summary>
    [Fact]
    public void Ascend_Cost_Clear_ReturnsBaseCost()
    {
        // Arrange: Jump up south (0,64,0) -> (0,65,1)
        // Need: clear at start body/head, clear jump clearance, solid platform at dest, clear dest body/head
        SetupAir(0, 64, 0);     // src body
        SetupAir(0, 65, 0);     // src head
        SetupAir(0, 66, 0);     // jump clearance
        SetupStone(0, 64, 1);   // dest platform
        SetupAir(0, 65, 1);     // dest body
        SetupAir(0, 66, 1);     // dest head
        
        var movement = new MovementAscend(0, 64, 0, 0, 1, MoveDirection.AscendSouth);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert: Should be finite (exact value depends on implementation)
        Assert.False(double.IsPositiveInfinity(cost), "Clear ascend should have finite cost");
        Assert.True(cost > 0, "Cost should be positive");
    }

    /// <summary>
    /// Test: Head block at destination adds mining cost.
    /// </summary>
    [Fact]
    public void Ascend_Cost_NeedBreakHead_AddsMiningCost()
    {
        // Arrange: Stone at dest head
        SetupAir(0, 64, 0);
        SetupAir(0, 65, 0);
        SetupAir(0, 66, 0);
        SetupStone(0, 64, 1);   // dest platform
        SetupAir(0, 65, 1);     // dest body - clear
        SetupStone(0, 66, 1);   // dest head - BLOCKED
        
        _context.GetBestToolSpeed = _ => 1.0f;
        
        var movement = new MovementAscend(0, 64, 0, 0, 1, MoveDirection.AscendSouth);

        // Act
        var blocksToBreak = movement.GetBlocksToBreak(_context).ToList();

        // Assert
        Assert.Contains((0, 66, 1), blocksToBreak);
    }

    /// <summary>
    /// Test: No platform (nothing to jump onto) and no blocks to place returns CostInf.
    /// </summary>
    [Fact]
    public void Ascend_Cost_NoPlatform_NoBlocks_ReturnsInfinity()
    {
        // Arrange: No platform at destination AND no blocks to place
        SetupAir(0, 64, 0);
        SetupAir(0, 65, 0);
        SetupAir(0, 66, 0);
        SetupAir(0, 64, 1);     // dest platform - AIR (no support!)
        SetupAir(0, 65, 1);
        SetupAir(0, 66, 1);
        
        // Use context without throwaway blocks so placement is not an option
        var contextNoBlocks = new CalculationContext(_level)
        {
            AllowBreak = true,
            AllowPlace = true,
            HasThrowaway = false  // No blocks to place!
        };
        
        var movement = new MovementAscend(0, 64, 0, 0, 1, MoveDirection.AscendSouth);

        // Act
        var cost = movement.CalculateCost(contextNoBlocks);

        // Assert
        Assert.Equal(ActionCosts.CostInf, cost);
    }

    /// <summary>
    /// Test: Jump clearance blocked returns blocks to break.
    /// </summary>
    [Fact]
    public void Ascend_GetBlocksToBreak_JumpClearanceBlocked()
    {
        // Arrange: Stone at jump clearance (y+2 from source)
        SetupAir(0, 64, 0);
        SetupAir(0, 65, 0);
        SetupStone(0, 66, 0);   // BLOCKED - would bonk head
        SetupStone(0, 64, 1);
        SetupAir(0, 65, 1);
        SetupAir(0, 66, 1);
        
        var movement = new MovementAscend(0, 64, 0, 0, 1, MoveDirection.AscendSouth);

        // Act
        var blocksToBreak = movement.GetBlocksToBreak(_context).ToList();

        // Assert
        Assert.Contains((0, 66, 0), blocksToBreak);
    }
}
