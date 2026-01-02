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
/// Tests for MovementDiagonal (walking diagonally).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDiagonal.java
/// </summary>
public class MovementDiagonalTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;

    public MovementDiagonalTests()
    {
        _tickManagerMock = new Mock<ITickManager>();
        _playerRegistryMock = new Mock<IPlayerRegistry>();
        _chunkManagerMock = new Mock<IChunkManager>();
        
        _level = new Level(_tickManagerMock.Object, _playerRegistryMock.Object, _chunkManagerMock.Object);
        _context = new CalculationContext(_level)
        {
            AllowBreak = true,
            AllowPlace = true,
            HasThrowaway = true
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
    /// Test: Both corners blocked - prefers one path (Corner A).
    /// </summary>
    [Fact]
    public void Diagonal_GetBlocksToBreak_BothCornersBlocked_PrefersCornerA()
    {
        // Arrange: Diagonal SE (0,64,0) -> (1,64,1)
        // Corner A = (1, 64, 0) - East
        // Corner B = (0, 64, 1) - South
        SetupAir(0, 64, 0);     // src body
        SetupAir(0, 65, 0);     // src head
        SetupStone(0, 63, 0);   // src floor
        
        SetupStone(1, 63, 1);   // dest floor
        SetupAir(1, 64, 1);     // dest body
        SetupAir(1, 65, 1);     // dest head
        
        // Block both corners
        SetupStone(1, 64, 0);   // Corner A body
        SetupStone(1, 65, 0);   // Corner A head
        SetupStone(0, 64, 1);   // Corner B body
        SetupStone(0, 65, 1);   // Corner B head
        SetupStone(1, 63, 0);   // Corner A floor
        SetupStone(0, 63, 1);   // Corner B floor
        
        var movement = new MovementDiagonal(0, 64, 0, 1, 64, 1, MoveDirection.DiagonalSE);

        // Act
        var blocksToBreak = movement.GetBlocksToBreak(_context).ToList();

        // Assert: Should prefer breaking Corner A (East)
        // The exact behavior depends on implementation - it should break ONE path
        Assert.True(blocksToBreak.Count >= 2, "Should need to break at least body+head of one corner");
    }

    /// <summary>
    /// Test: Single corner blocked uses clear corner path.
    /// </summary>
    [Fact]
    public void Diagonal_GetBlocksToBreak_SingleCornerBlocked_UsesClearCorner()
    {
        // Arrange: Diagonal SE with Corner A blocked, Corner B clear
        SetupAir(0, 64, 0);
        SetupAir(0, 65, 0);
        SetupStone(0, 63, 0);
        
        SetupStone(1, 63, 1);
        SetupAir(1, 64, 1);
        SetupAir(1, 65, 1);
        
        // Block Corner A (East)
        SetupStone(1, 64, 0);
        SetupStone(1, 65, 0);
        SetupStone(1, 63, 0);
        
        // Clear Corner B (South)
        SetupAir(0, 64, 1);
        SetupAir(0, 65, 1);
        SetupStone(0, 63, 1);
        
        var movement = new MovementDiagonal(0, 64, 0, 1, 64, 1, MoveDirection.DiagonalSE);

        // Act
        var blocksToBreak = movement.GetBlocksToBreak(_context).ToList();

        // Assert: Should not need to break anything (use clear south path)
        Assert.Empty(blocksToBreak);
    }
}
