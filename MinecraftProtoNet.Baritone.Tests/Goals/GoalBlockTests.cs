using FluentAssertions;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Pathfinding.Goals;

namespace MinecraftProtoNet.Baritone.Tests.Goals;

/// <summary>
/// Tests for GoalBlock - exact block position targeting.
/// </summary>
public class GoalBlockTests
{
    [Fact]
    public void IsInGoal_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var goal = new GoalBlock(10, 64, 20);
        
        // Act & Assert
        goal.IsInGoal(10, 64, 20).Should().BeTrue();
    }
    
    [Theory]
    [InlineData(11, 64, 20)]  // X off by 1
    [InlineData(10, 65, 20)]  // Y off by 1
    [InlineData(10, 64, 21)]  // Z off by 1
    [InlineData(0, 0, 0)]     // Completely different
    public void IsInGoal_NotAtGoal_ReturnsFalse(int x, int y, int z)
    {
        // Arrange
        var goal = new GoalBlock(10, 64, 20);
        
        // Act & Assert
        goal.IsInGoal(x, y, z).Should().BeFalse();
    }
    
    [Fact]
    public void Heuristic_AtGoal_ReturnsZero()
    {
        // Arrange
        var goal = new GoalBlock(10, 64, 20);
        
        // Act
        var heuristic = goal.Heuristic(10, 64, 20);
        
        // Assert
        heuristic.Should().Be(0);
    }
    
    [Fact]
    public void Heuristic_FartherAway_ReturnsHigherValue()
    {
        // Arrange
        var goal = new GoalBlock(10, 64, 20);
        
        // Act
        var close = goal.Heuristic(11, 64, 20);  // 1 block away
        var far = goal.Heuristic(20, 64, 20);    // 10 blocks away
        
        // Assert
        far.Should().BeGreaterThan(close);
    }
    
    [Fact]
    public void Heuristic_Ascending_CostsLessThanDescending()
    {
        // Arrange - goal at Y=64
        var goal = new GoalBlock(10, 64, 20);
        
        // Act
        var ascending = goal.Heuristic(10, 60, 20);   // Need to go UP 4 blocks
        var descending = goal.Heuristic(10, 68, 20);  // Need to go DOWN 4 blocks
        
        // Assert - in Baritone's heuristic, falling uses GetFallCost which can be higher
        // than jumping's JumpOneBlockCost for short heights
        descending.Should().BeGreaterThan(ascending);
    }
    
    [Fact]
    public void Heuristic_Diagonal_CheaperThanManhattan()
    {
        // Arrange
        var goal = new GoalBlock(0, 64, 0);
        
        // Act
        // Diagonal movement from (10, 64, 10) should use sqrt(2) * 10 ≈ 14.14
        // Two separate movements (10, 64, 0) + (0, 64, 10) would be straighter = 20
        var diagonal = goal.Heuristic(10, 64, 10);
        var straightX = goal.Heuristic(10, 64, 0);
        var straightZ = goal.Heuristic(0, 64, 10);
        
        // Assert
        diagonal.Should().BeLessThan(straightX + straightZ);
    }
    
    [Fact]
    public void Heuristic_NegativeCoordinates_Works()
    {
        // Arrange
        var goal = new GoalBlock(-100, 64, -100);
        
        // Act
        var heuristic = goal.Heuristic(-100, 64, -100);
        
        // Assert
        heuristic.Should().Be(0);
    }
    
    [Fact]
    public void ToString_ReturnsReadableFormat()
    {
        // Arrange
        var goal = new GoalBlock(10, 64, 20);
        
        // Act & Assert
        goal.ToString().Should().Be("GoalBlock(10, 64, 20)");
    }
}
