using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using Moq;
using Xunit;

namespace MinecraftProtoNet.Baritone.Tests.Pathfinding;

public class MovementTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;

    public MovementTests()
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

    private void SetupBlock(int x, int y, int z, string name, bool blocksMotion = true)
    {
        var block = new BlockState(1, name, new Dictionary<string, string>())
        {
            HasCollision = blocksMotion
        };
        
        // Mock chunk manager behavior
        // Since Level delegates to ChunkManager, we need to mock GetBlockAt
        _chunkManagerMock.Setup(m => m.GetBlockAt(x, y, z)).Returns(block);
    }

    private void SetupAir(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:air", false);
    private void SetupStone(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:stone", true);

    [Fact]
    public void TestTraverse_Break()
    {
        // Arrange: Wall at destination
        var start = (X: 0, Y: 64, Z: 0);
        var dest = (X: 0, Y: 64, Z: 1); // South
        
        SetupAir(0, 64, 0); SetupAir(0, 65, 0); // Start clear
        SetupStone(0, 64, 1); SetupStone(0, 65, 1); // Wall at dest body+head
        
        var movement = new MovementTraverse(start.X, start.Y, start.Z, dest.X, dest.Z, MoveDirection.TraverseSouth);

        // Act
        var blocksToBreak = movement.GetBlocksToBreak(_context).ToList();

        // Assert
        Assert.Contains((0, 64, 1), blocksToBreak);
        Assert.Contains((0, 65, 1), blocksToBreak);
        Assert.Equal(2, blocksToBreak.Count);
    }

    [Fact]
    public void TestAscend_Break()
    {
        // Arrange: Jump up south
        var start = (X: 0, Y: 64, Z: 0);
        var dest = (X: 0, Y: 65, Z: 1);
        
        SetupAir(0, 64, 0); SetupAir(0, 65, 0);
        SetupStone(0, 66, 0); // Head bonk at start (jump clearance)
        SetupStone(0, 65, 1); // Blocked body at dest
        SetupAir(0, 66, 1); // Clear head at dest
        
        var movement = new MovementAscend(start.X, start.Y, start.Z, dest.X, dest.Z, MoveDirection.AscendSouth);

        // Act
        var blocksToBreak = movement.GetBlocksToBreak(_context).ToList();

        // Assert
        Assert.Contains((0, 66, 0), blocksToBreak); // Jump clearance
        Assert.Contains((0, 65, 1), blocksToBreak); // Dest body
        Assert.Equal(2, blocksToBreak.Count);
    }

    [Fact]
    public void TestDescend_Break()
    {
        // Arrange: Step down south
        var start = (X: 0, Y: 65, Z: 0);
        var dest = (X: 0, Y: 64, Z: 1);
        
        SetupAir(0, 65, 0); SetupAir(0, 66, 0);
        SetupStone(0, 64, 1); // Blocked body at dest
        SetupAir(0, 65, 1);   // Clear head at dest
        SetupStone(0, 66, 1); // Blocked forward head (above ledge)

        var movement = new MovementDescend(start.X, start.Y, start.Z, dest.X, dest.Z, MoveDirection.DescendSouth);
        
        // Act
        var blocksToBreak = movement.GetBlocksToBreak(_context).ToList();
        
        // Assert
        Assert.Contains((0, 64, 1), blocksToBreak); // Dest body
        Assert.Contains((0, 66, 1), blocksToBreak); // Forward head clearance
        Assert.Equal(2, blocksToBreak.Count);
    }
    
    [Fact]
    public void TestDescend_Place()
    {
        // Arrange: Step down but floor missing
        var start = (X: 0, Y: 65, Z: 0);
        var dest = (X: 0, Y: 64, Z: 1);
        
        SetupAir(0, 63, 1); // Hole at destination floor
        
        var movement = new MovementDescend(start.X, start.Y, start.Z, dest.X, dest.Z, MoveDirection.DescendSouth);
        
        // Act
        var blocksToPlace = movement.GetBlocksToPlace(_context).ToList();
        
        // Assert
        Assert.Contains((0, 63, 1), blocksToPlace);
        Assert.Single(blocksToPlace);
    }
    
    [Fact]
    public void TestDiagonal_Break_BothCornersBlocked()
    {
        // Arrange: Diagonal SE
        var start = (X: 0, Y: 64, Z: 0);
        var dest = (X: 1, Y: 64, Z: 1);
        
        SetupAir(1, 64, 1); SetupAir(1, 65, 1); // Clear dest
        
        // Block both corners
        SetupStone(1, 64, 0); SetupStone(1, 65, 0); // Corner A (East) blocked
        SetupStone(0, 64, 1); SetupStone(0, 65, 1); // Corner B (South) blocked
        
        var movement = new MovementDiagonal(start.X, start.Y, start.Z, dest.X, dest.Y, dest.Z, MoveDirection.DiagonalSE);
        
        // Act
        var blocksToBreak = movement.GetBlocksToBreak(_context).ToList();
        
        // Assert: Should break Corner A (East) preferentially
        Assert.Contains((1, 64, 0), blocksToBreak);
        Assert.Contains((1, 65, 0), blocksToBreak);
        Assert.DoesNotContain((0, 64, 1), blocksToBreak); // Should not try to break both, just one path
    }
}
