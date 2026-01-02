using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Pathfinding;
using MinecraftProtoNet.State;
using Moq;
using Xunit;

namespace MinecraftProtoNet.Baritone.Tests.Pathfinding;

/// <summary>
/// Tests for MovementHelper methods.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementHelper.java
/// </summary>
public class MovementHelperTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly CalculationContext _context;

    public MovementHelperTests()
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

    private void SetupBlock(int x, int y, int z, string name, bool hasCollision = true)
    {
        var block = new BlockState(1, name, new Dictionary<string, string>())
        {
            HasCollision = hasCollision
        };
        _chunkManagerMock.Setup(m => m.GetBlockAt(x, y, z)).Returns(block);
    }

    private BlockState CreateBlock(string name, bool hasCollision = true)
    {
        return new BlockState(1, name, new Dictionary<string, string>())
        {
            HasCollision = hasCollision
        };
    }

    // ===== FullyPassable Tests =====

    [Fact]
    public void FullyPassable_Air_ReturnsTrue()
    {
        var air = CreateBlock("minecraft:air", false);
        Assert.True(MovementHelper.FullyPassable(air));
    }

    [Fact]
    public void FullyPassable_Water_ReturnsFalse()
    {
        var water = CreateBlock("minecraft:water", false);  // IsLiquid computed from name
        Assert.False(MovementHelper.FullyPassable(water));
    }

    [Fact]
    public void FullyPassable_Ladder_ReturnsFalse()
    {
        var ladder = CreateBlock("minecraft:ladder", false);
        Assert.False(MovementHelper.FullyPassable(ladder));
    }

    [Fact]
    public void FullyPassable_Door_ReturnsFalse()
    {
        var door = CreateBlock("minecraft:oak_door", true);
        Assert.False(MovementHelper.FullyPassable(door));
    }

    [Fact]
    public void FullyPassable_Cobweb_ReturnsFalse()
    {
        var cobweb = CreateBlock("minecraft:cobweb", false);
        Assert.False(MovementHelper.FullyPassable(cobweb));
    }

    // ===== CanPlaceAgainst Tests =====

    [Fact]
    public void CanPlaceAgainst_Stone_ReturnsTrue()
    {
        var stone = CreateBlock("minecraft:stone", true);
        Assert.True(MovementHelper.CanPlaceAgainst(stone));
    }

    [Fact]
    public void CanPlaceAgainst_Glass_ReturnsTrue()
    {
        var glass = CreateBlock("minecraft:glass", true);
        Assert.True(MovementHelper.CanPlaceAgainst(glass));
    }

    [Fact]
    public void CanPlaceAgainst_Slab_ReturnsFalse()
    {
        var slab = CreateBlock("minecraft:stone_slab", true);
        Assert.False(MovementHelper.CanPlaceAgainst(slab));
    }

    [Fact]
    public void CanPlaceAgainst_Fence_ReturnsFalse()
    {
        var fence = CreateBlock("minecraft:oak_fence", true);
        Assert.False(MovementHelper.CanPlaceAgainst(fence));
    }

    // ===== IsBlockNormalCube Tests =====

    [Fact]
    public void IsBlockNormalCube_Stone_ReturnsTrue()
    {
        var stone = CreateBlock("minecraft:stone", true);
        Assert.True(MovementHelper.IsBlockNormalCube(stone));
    }

    [Fact]
    public void IsBlockNormalCube_Stair_ReturnsFalse()
    {
        var stair = CreateBlock("minecraft:stone_stairs", true);
        Assert.False(MovementHelper.IsBlockNormalCube(stair));
    }

    [Fact]
    public void IsBlockNormalCube_Chest_ReturnsFalse()
    {
        var chest = CreateBlock("minecraft:chest", true);
        Assert.False(MovementHelper.IsBlockNormalCube(chest));
    }

    [Fact]
    public void IsBlockNormalCube_Air_ReturnsFalse()
    {
        var air = CreateBlock("minecraft:air", false);
        Assert.False(MovementHelper.IsBlockNormalCube(air));
    }

    // ===== AvoidAdjacentBreaking Tests =====

    [Fact]
    public void AvoidAdjacentBreaking_LiquidAbove_ReturnsTrue()
    {
        SetupBlock(0, 65, 0, "minecraft:water", false);  // IsLiquid computed from name
        
        Assert.True(MovementHelper.AvoidAdjacentBreaking(_context, 0, 65, 0, true));
    }

    [Fact]
    public void AvoidAdjacentBreaking_UnsupportedFallingBlock_ReturnsTrue()
    {
        SetupBlock(0, 65, 0, "minecraft:sand", true);
        SetupBlock(0, 64, 0, "minecraft:air", false);  // Nothing supporting the sand
        
        Assert.True(MovementHelper.AvoidAdjacentBreaking(_context, 0, 65, 0, false));
    }

    [Fact]
    public void AvoidAdjacentBreaking_SupportedFallingBlock_ReturnsFalse()
    {
        SetupBlock(0, 65, 0, "minecraft:sand", true);
        SetupBlock(0, 64, 0, "minecraft:stone", true);  // Stone supporting the sand
        
        Assert.False(MovementHelper.AvoidAdjacentBreaking(_context, 0, 65, 0, false));
    }

    // ===== AvoidBreaking Tests =====

    [Fact]
    public void AvoidBreaking_Ice_ReturnsTrue()
    {
        var ice = CreateBlock("minecraft:ice", true);
        SetupBlock(0, 65, 0, "minecraft:air", false);  // Above
        SetupBlock(1, 64, 0, "minecraft:stone", true);  // Adjacent
        SetupBlock(-1, 64, 0, "minecraft:stone", true);
        SetupBlock(0, 64, 1, "minecraft:stone", true);
        SetupBlock(0, 64, -1, "minecraft:stone", true);
        
        Assert.True(MovementHelper.AvoidBreaking(_context, 0, 64, 0, ice));
    }

    [Fact]
    public void AvoidBreaking_InfestedBlock_ReturnsTrue()
    {
        var infested = CreateBlock("minecraft:infested_stone", true);
        SetupBlock(0, 65, 0, "minecraft:air", false);
        SetupBlock(1, 64, 0, "minecraft:stone", true);
        SetupBlock(-1, 64, 0, "minecraft:stone", true);
        SetupBlock(0, 64, 1, "minecraft:stone", true);
        SetupBlock(0, 64, -1, "minecraft:stone", true);
        
        Assert.True(MovementHelper.AvoidBreaking(_context, 0, 64, 0, infested));
    }

    [Fact]
    public void AvoidBreaking_PackedIce_ReturnsFalse()
    {
        var packedIce = CreateBlock("minecraft:packed_ice", true);
        SetupBlock(0, 65, 0, "minecraft:air", false);
        SetupBlock(1, 64, 0, "minecraft:stone", true);
        SetupBlock(-1, 64, 0, "minecraft:stone", true);
        SetupBlock(0, 64, 1, "minecraft:stone", true);
        SetupBlock(0, 64, -1, "minecraft:stone", true);
        
        Assert.False(MovementHelper.AvoidBreaking(_context, 0, 64, 0, packedIce));
    }
}
