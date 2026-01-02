using MinecraftProtoNet.Baritone.Pathfinding;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Pathfinding;
using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MinecraftProtoNet.Pathfinding.Goals;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;

namespace MinecraftProtoNet.Baritone.Tests.Pathfinding;

public class PathExecutorTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;
    private readonly InputState _inputState;
    private readonly Entity _testEntity;
    private readonly Mock<IGoal> _goalMock;

    public PathExecutorTests()
    {
        _tickManagerMock = new Mock<ITickManager>();
        _playerRegistryMock = new Mock<IPlayerRegistry>();
        _chunkManagerMock = new Mock<IChunkManager>();
        _goalMock = new Mock<IGoal>();

        _level = new Level(_tickManagerMock.Object, _playerRegistryMock.Object, _chunkManagerMock.Object);
        _context = new CalculationContext(_level) { AllowBreak = true };
        
        // Setup simple entity
        _testEntity = new Entity
        {
            EntityId = 1,
            Position = new Vector3<double>(0, 64, 0)
        };
        _inputState = _testEntity.InputState;
    }

    private void SetupBlock(int x, int y, int z, string name, bool blocksMotion = true)
    {
        var block = new BlockState(1, name, new Dictionary<string, string>())
        {
            HasCollision = blocksMotion
        };
        _chunkManagerMock.Setup(m => m.GetBlockAt(x, y, z)).Returns(block);
    }

    [Fact]
    public void TestProcessHorizon_ProactiveBreak()
    {
        // Arrange: A path that walks into a wall 2 steps away
        var path = new Path(new List<(int X, int Y, int Z)>
        {
            (0, 64, 0), (0, 64, 1), (0, 64, 2)
        }, _goalMock.Object, 0, true);

        // Wall at (0, 64, 2)
        SetupBlock(0, 64, 1, "minecraft:air", false);
        SetupBlock(0, 65, 1, "minecraft:air", false);
        SetupBlock(0, 64, 2, "minecraft:stone", true); // Wall

        var executor = new PathExecutor(new NullLogger<PathExecutor>(), path, _context);
        
        bool breakRequested = false;
        executor.OnBreakBlockRequest = (x, y, z) =>
        {
            if (x == 0 && y == 64 && z == 2) breakRequested = true;
            return true;
        };

        // Act: Tick the executor
        // It's at (0, 64, 0). Next move is traverse to (0, 64, 1).
        // Horizon should see the wall at (0, 64, 2) from next movement.
        executor.OnTick(_testEntity, _level);

        // Assert
        Assert.True(breakRequested, "ProcessHorizon should request break for future wall");
    }

    [Fact]
    public void TestRuntimeVerification_AbortsOnBlock()
    {
        // Arrange: Path originally clear, but now blocked
        // We need 2 segments: 0->1 (current), 1->2 (future/blocked)
        var path = new Path(new List<(int X, int Y, int Z)>
        {
            (0, 64, 0), (0, 64, 1), (0, 64, 2)
        }, _goalMock.Object, 10, true);

        // Current move (0->1) is clear
        SetupBlock(0, 64, 1, "minecraft:air", false);
        
        // Future move (1->2) blocked by BEDROCK at (0, 64, 2)
        SetupBlock(0, 64, 2, "minecraft:bedrock", true);
        var block = _level.GetBlockAt(0, 64, 2);
        block!.DestroySpeed = -1.0f; // Unbreakable, cost = inf

        var executor = new PathExecutor(new NullLogger<PathExecutor>(), path, _context);

        // Act
        executor.OnTick(_testEntity, _level);

        // Assert
        Assert.True(executor.Failed, "Should fail due to infinite cost");
        Assert.True(executor.Finished, "Should finish (abort)");
    }

    [Fact]
    public void TestSkipToAscend_DetectsBlockedHead()
    {
        // Verify the logic inside MovementDescend directly
        var mov = new MovementDescend(0, 65, 0, 0, 1, MoveDirection.DescendSouth);
        
        // Setup blocked head at destination
        SetupBlock(0, 64, 1, "minecraft:air", false); // Dest floor (at Y=63 is stone, air here)
        SetupBlock(0, 65, 1, "minecraft:stone", true); // Head blocked at dest

        // Act
        bool skip = mov.SkipToAscend(_level);

        // Assert
        Assert.True(skip, "Should detect blocked head and request skip/safe mode");
    }
}
