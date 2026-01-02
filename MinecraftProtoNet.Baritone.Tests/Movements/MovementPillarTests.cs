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
/// Tests for MovementPillar (towering up one block).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/movements/MovementPillar.java
/// </summary>
public class MovementPillarTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;

    public MovementPillarTests()
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
            JumpPenalty = 2.0,
            PlaceBlockCost = 0
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
    private void SetupWater(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:water", false, 0);
    private void SetupLadder(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:ladder", false, 0.4f);
    private void SetupFenceGate(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:oak_fence_gate", true, 2.0f);
    private void SetupSand(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:sand", true, 0.5f);
    private void SetupBedrock(int x, int y, int z) => SetupBlock(x, y, z, "minecraft:bedrock", true, -1f);

    /// <summary>
    /// Test: Clear headspace returns base pillar cost (JumpOneBlockCost + PlaceCost + JumpPenalty).
    /// </summary>
    [Fact]
    public void Pillar_Cost_ClearAbove_ReturnsBaseCost()
    {
        // Arrange: Clear above, solid floor
        SetupAir(0, 64, 0);     // standing position
        SetupStone(0, 63, 0);   // floor (solid)
        SetupAir(0, 65, 0);     // head
        SetupAir(0, 66, 0);     // above head (y+2)
        
        var movement = new MovementPillar(0, 64, 0);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert: Should be JumpOneBlockCost + PlaceBlockCost + JumpPenalty
        var expectedCost = ActionCosts.JumpOneBlockCost + _context.PlaceBlockCost + _context.JumpPenalty;
        Assert.Equal(expectedCost, cost, precision: 3);
    }

    /// <summary>
    /// Test: Block at y+2 adds mining duration to cost.
    /// </summary>
    [Fact]
    public void Pillar_Cost_BlockAbove_AddsMiningCost()
    {
        // Arrange: Stone at y+2, solid floor
        SetupAir(0, 64, 0);
        SetupStone(0, 63, 0);
        SetupAir(0, 65, 0);
        SetupStone(0, 66, 0);   // Block that needs breaking
        SetupAir(0, 67, 0);     // y+3 (no falling block)
        
        var movement = new MovementPillar(0, 64, 0);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert: Cost should include mining time (greater than base cost)
        var baseCost = ActionCosts.JumpOneBlockCost + _context.PlaceBlockCost + _context.JumpPenalty;
        Assert.True(cost > baseCost, $"Cost {cost} should be greater than base cost {baseCost}");
        Assert.False(double.IsPositiveInfinity(cost), "Cost should not be infinite for breakable block");
    }

    /// <summary>
    /// Test: Fence gate above returns CostInf (Baritone issue #172).
    /// </summary>
    [Fact]
    public void Pillar_Cost_FenceGateAbove_ReturnsInfinity()
    {
        // Arrange: Fence gate at y+2
        SetupAir(0, 64, 0);
        SetupStone(0, 63, 0);
        SetupAir(0, 65, 0);
        SetupFenceGate(0, 66, 0);   // Fence gate blocks pillar
        
        var movement = new MovementPillar(0, 64, 0);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert
        Assert.Equal(ActionCosts.CostInf, cost);
    }

    /// <summary>
    /// Test: Water column ascent returns LadderUpOneCost.
    /// </summary>
    [Fact]
    public void Pillar_Cost_WaterColumn_ReturnsLadderCost()
    {
        // Arrange: Full water column
        SetupWater(0, 64, 0);   // standing in water
        SetupStone(0, 63, 0);   // floor
        SetupWater(0, 65, 0);   // y+1 water
        SetupWater(0, 66, 0);   // y+2 water
        
        var movement = new MovementPillar(0, 64, 0);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert
        Assert.Equal(ActionCosts.LadderUpOneCost, cost, precision: 3);
    }

    /// <summary>
    /// Test: Falling block above non-chain returns CostInf.
    /// </summary>
    [Fact]
    public void Pillar_Cost_FallingBlockAbove_ReturnsInfinity()
    {
        // Arrange: Sand at y+3 above stone at y+2 (dangerous configuration)
        SetupAir(0, 64, 0);
        SetupStone(0, 63, 0);
        SetupAir(0, 65, 0);
        SetupStone(0, 66, 0);   // y+2 - stone (not falling)
        SetupSand(0, 67, 0);    // y+3 - sand (falling) will fall on us

        // Enable tool speed callback (returns 1.0 for all blocks)
        _context.GetBestToolSpeed = _ => 1.0f;
        
        var movement = new MovementPillar(0, 64, 0);

        // Act
        var cost = movement.CalculateCost(_context);

        // Assert: Should be infinite because breaking stone will cause sand to fall
        Assert.Equal(ActionCosts.CostInf, cost);
    }

    /// <summary>
    /// Test: No throwaway blocks returns CostInf.
    /// </summary>
    [Fact]
    public void Pillar_Cost_NoThrowaway_ReturnsInfinity()
    {
        // Arrange: No blocks to place
        SetupAir(0, 64, 0);
        SetupStone(0, 63, 0);
        SetupAir(0, 65, 0);
        SetupAir(0, 66, 0);
        
        var contextNoBlocks = new CalculationContext(_level)
        {
            AllowPlace = true,
            HasThrowaway = false  // No blocks to place!
        };
        
        var movement = new MovementPillar(0, 64, 0);

        // Act
        var cost = movement.CalculateCost(contextNoBlocks);

        // Assert
        Assert.Equal(ActionCosts.CostInf, cost);
    }
}
