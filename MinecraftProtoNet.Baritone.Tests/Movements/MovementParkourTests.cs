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
/// Tests for MovementParkour (jumping across gaps).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementParkour.java
/// </summary>
public class MovementParkourTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;

    public MovementParkourTests()
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
            AllowParkour = true,
            CanSprint = true
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
    /// Test: 2-block gap jump has finite cost.
    /// </summary>
    [Fact]
    public void Parkour_Cost_2BlockGap_HasFiniteCost()
    {
        // Arrange: Jump south 2 blocks (0,64,0) -> (0,64,2)
        // Gap at z=1
        SetupAir(0, 64, 0);     // src body
        SetupAir(0, 65, 0);     // src head
        SetupStone(0, 63, 0);   // src floor
        
        SetupAir(0, 63, 1);     // gap floor
        SetupAir(0, 64, 1);     // gap body
        SetupAir(0, 65, 1);     // gap head
        
        SetupStone(0, 63, 2);   // dest floor
        SetupAir(0, 64, 2);     // dest body
        SetupAir(0, 65, 2);     // dest head
        
        var movement = new MovementParkour(0, 64, 0, 0, 64, 2, MoveDirection.ParkourSouth, 2);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert
        Assert.False(double.IsPositiveInfinity(cost), "2-block gap should be jumpable");
        Assert.True(cost > 0, "Cost should be positive");
    }

    /// <summary>
    /// Test: 3-block gap requires sprint, has higher cost.
    /// </summary>
    [Fact]
    public void Parkour_Cost_3BlockGap_RequiresSprint()
    {
        // Arrange: Jump south 3 blocks (0,64,0) -> (0,64,3)
        SetupAir(0, 64, 0);
        SetupAir(0, 65, 0);
        SetupStone(0, 63, 0);
        
        for (int z = 1; z <= 2; z++)
        {
            SetupAir(0, 63, z);
            SetupAir(0, 64, z);
            SetupAir(0, 65, z);
        }
        
        SetupStone(0, 63, 3);
        SetupAir(0, 64, 3);
        SetupAir(0, 65, 3);
        
        var movementSprint = new MovementParkour(0, 64, 0, 0, 64, 3, MoveDirection.ParkourSouth, 3);
        
        // Also test with sprint disabled
        var contextNoSprint = new CalculationContext(_level)
        {
            AllowParkour = true,
            CanSprint = false
        };
        
        var costWithSprint = movementSprint.CalculateCost(_context);
        var costNoSprint = movementSprint.CalculateCost(contextNoSprint);

        // Assert: 3-block jump should fail without sprint
        Assert.False(double.IsPositiveInfinity(costWithSprint), "3-block gap should be possible with sprint");
    }

    /// <summary>
    /// Test: 4-block gap is impossible (too far).
    /// </summary>
    [Fact]
    public void Parkour_Cost_4BlockGap_ReturnsInfinity()
    {
        // Arrange: 4-block gap (impossible)
        SetupAir(0, 64, 0);
        SetupAir(0, 65, 0);
        SetupStone(0, 63, 0);
        
        for (int z = 1; z <= 3; z++)
        {
            SetupAir(0, 63, z);
            SetupAir(0, 64, z);
            SetupAir(0, 65, z);
        }
        
        SetupStone(0, 63, 4);
        SetupAir(0, 64, 4);
        SetupAir(0, 65, 4);
        
        var movement = new MovementParkour(0, 64, 0, 0, 64, 4, MoveDirection.ParkourSouth, 4);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert
        Assert.Equal(ActionCosts.CostInf, cost);
    }
}
