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
/// Tests for MovementDownward (digging straight down).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementDownward.java
/// </summary>
public class MovementDownwardTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;

    public MovementDownwardTests()
    {
        _tickManagerMock = new Mock<ITickManager>();
        _playerRegistryMock = new Mock<IPlayerRegistry>();
        _chunkManagerMock = new Mock<IChunkManager>();
        
        _level = new Level(_tickManagerMock.Object, _playerRegistryMock.Object, _chunkManagerMock.Object);
        _context = new CalculationContext(_level)
        {
            AllowBreak = true,
            AllowDownward = true
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
    private void SetupBedrock(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:bedrock", true, -1f);

    /// <summary>
    /// Test: Downward mining cost includes block break time.
    /// </summary>
    [Fact]
    public void Downward_Cost_Mining_IncludesBreakTime()
    {
        // Arrange: Dig down through stone
        SetupAir(0, 64, 0);     // standing body
        SetupAir(0, 65, 0);     // standing head
        SetupStone(0, 63, 0);   // floor (to break)
        SetupStone(0, 62, 0);   // new floor
        
        _context.GetBestToolSpeed = _ => 1.0f;
        
        var movement = new MovementDownward(0, 64, 0);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert
        Assert.False(double.IsPositiveInfinity(cost), "Stone should be breakable");
        Assert.True(cost > 0, "Cost should include mining time");
    }

    /// <summary>
    /// Test: Bedrock (unbreakable) returns CostInf.
    /// </summary>
    [Fact]
    public void Downward_Cost_Bedrock_ReturnsInfinity()
    {
        // Arrange: Bedrock below
        SetupAir(0, 64, 0);
        SetupAir(0, 65, 0);
        SetupBedrock(0, 63, 0); // Unbreakable!
        SetupStone(0, 62, 0);
        
        var movement = new MovementDownward(0, 64, 0);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert
        Assert.Equal(ActionCosts.CostInf, cost);
    }
}
