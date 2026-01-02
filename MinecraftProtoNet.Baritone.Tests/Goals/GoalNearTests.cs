using FluentAssertions;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Pathfinding.Goals;

namespace MinecraftProtoNet.Baritone.Tests.Goals;

/// <summary>
/// Tests for GoalNear - proximity-based goal.
/// </summary>
public class GoalNearTests
{
    [Fact]
    public void IsInGoal_AtCenter_ReturnsTrue()
    {
        // Arrange
        var goal = new GoalNear(10, 64, 20, range: 5);
        
        // Act & Assert
        goal.IsInGoal(10, 64, 20).Should().BeTrue();
    }
    
    [Fact]
    public void IsInGoal_WithinRange_ReturnsTrue()
    {
        // Arrange - range of 5
        var goal = new GoalNear(10, 64, 20, range: 5);
        
        // Act & Assert - 3 blocks away in X
        goal.IsInGoal(13, 64, 20).Should().BeTrue();
    }
    
    [Fact]
    public void IsInGoal_ExactlyAtRange_ReturnsTrue()
    {
        // Arrange - range of 5, so 5 blocks away should be in goal
        var goal = new GoalNear(10, 64, 20, range: 5);
        
        // Act & Assert - exactly 5 blocks in X
        goal.IsInGoal(15, 64, 20).Should().BeTrue();
    }
    
    [Fact]
    public void IsInGoal_OutsideRange_ReturnsFalse()
    {
        // Arrange - range of 3
        var goal = new GoalNear(10, 64, 20, range: 3);
        
        // Act & Assert - 5 blocks away
        goal.IsInGoal(15, 64, 20).Should().BeFalse();
    }
    
    [Fact]
    public void IsInGoal_DiagonalDistance_UsesEuclidean()
    {
        // Arrange - range of 2
        var goal = new GoalNear(0, 64, 0, range: 2);
        
        // 2 blocks in X and 2 in Z = sqrt(8) ≈ 2.83, which is > 2
        goal.IsInGoal(2, 64, 2).Should().BeFalse();
        
        // 1 block in X and 1 in Z = sqrt(2) ≈ 1.41, which is <= 2
        goal.IsInGoal(1, 64, 1).Should().BeTrue();
    }
    
    [Fact]
    public void IsInGoal_IncludesYDimension()
    {
        // Arrange - range of 3
        var goal = new GoalNear(10, 64, 20, range: 3);
        
        // 3 blocks above, 0 in X/Z = distance of 3
        goal.IsInGoal(10, 67, 20).Should().BeTrue();
        
        // 4 blocks above = distance of 4 > 3
        goal.IsInGoal(10, 68, 20).Should().BeFalse();
    }
    
    [Fact]
    public void Heuristic_AtCenter_ReturnsZero()
    {
        // Arrange
        var goal = new GoalNear(10, 64, 20, range: 5);
        
        // Act
        var heuristic = goal.Heuristic(10, 64, 20);
        
        // Assert
        heuristic.Should().Be(0);
    }
    
    [Fact]
    public void Heuristic_WithinRange_StillPullsToCenter()
    {
        // Arrange - within the 5 block range
        var goal = new GoalNear(10, 64, 20, range: 5);
        
        // Act - at edge of range (3 blocks away)
        var atEdge = goal.Heuristic(13, 64, 20);
        var atCenter = goal.Heuristic(10, 64, 20);
        
        // Assert - heuristic should still be non-zero even when in goal
        // (Baritone behavior: pull towards center)
        atEdge.Should().BeGreaterThan(atCenter);
    }
    
    [Fact]
    public void RangeSquared_StoresSquaredValue()
    {
        // Arrange
        var goal = new GoalNear(0, 0, 0, range: 10);
        
        // Assert
        goal.RangeSquared.Should().Be(100);
    }
    
    [Fact]
    public void ToString_IncludesRangeInfo()
    {
        // Arrange
        var goal = new GoalNear(10, 64, 20, range: 5);
        
        // Act
        var str = goal.ToString();
        
        // Assert
        str.Should().Contain("GoalNear");
        str.Should().Contain("10");
        str.Should().Contain("64");
        str.Should().Contain("20");
        str.Should().Contain("5");
    }
}
