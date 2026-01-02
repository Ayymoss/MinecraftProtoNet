using FluentAssertions;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;
using MinecraftProtoNet.Pathfinding.Calc;

namespace MinecraftProtoNet.Baritone.Tests.Calc;

/// <summary>
/// Tests for PathNode - A* graph node.
/// </summary>
public class PathNodeTests
{
    [Fact]
    public void Constructor_SetsCoordinates()
    {
        // Arrange & Act
        var node = new PathNode(10, 64, 20);
        
        // Assert
        node.X.Should().Be(10);
        node.Y.Should().Be(64);
        node.Z.Should().Be(20);
    }
    
    [Fact]
    public void Constructor_DefaultValues_AreInfinity()
    {
        // Arrange & Act
        var node = new PathNode(0, 0, 0);
        
        // Assert
        node.Cost.Should().Be(ActionCosts.CostInf);
        node.CombinedCost.Should().Be(ActionCosts.CostInf);
        node.Previous.Should().BeNull();
    }
    
    [Fact]
    public void IsOpen_DefaultHeapIndex_ReturnsFalse()
    {
        // Arrange
        var node = new PathNode(0, 0, 0);
        
        // Assert
        node.HeapIndex.Should().Be(-1);
        node.IsOpen().Should().BeFalse();
    }
    
    [Fact]
    public void IsOpen_WhenInHeap_ReturnsTrue()
    {
        // Arrange
        var node = new PathNode(0, 0, 0) { HeapIndex = 5 };
        
        // Assert
        node.IsOpen().Should().BeTrue();
    }
    
    [Fact]
    public void HashCode_SameCoordinates_SameHash()
    {
        // Arrange
        var node1 = new PathNode(100, 64, 200);
        var node2 = new PathNode(100, 64, 200);
        
        // Assert
        node1.HashCode.Should().Be(node2.HashCode);
    }
    
    [Fact]
    public void HashCode_DifferentCoordinates_DifferentHash()
    {
        // Arrange
        var node1 = new PathNode(100, 64, 200);
        var node2 = new PathNode(101, 64, 200);
        
        // Assert
        node1.HashCode.Should().NotBe(node2.HashCode);
    }
    
    [Fact]
    public void CalculateHash_NegativeCoordinates_Works()
    {
        // Arrange & Act - should not throw
        var hash1 = PathNode.CalculateHash(-1000, -64, -1000);
        var hash2 = PathNode.CalculateHash(-1000, -64, -1001);
        
        // Assert
        hash1.Should().NotBe(hash2);
    }
    
    [Fact]
    public void CalculateHash_ExtremeCoordinates_Works()
    {
        // Arrange - test near Minecraft world border
        var hash1 = PathNode.CalculateHash(29999999, 320, 29999999);
        var hash2 = PathNode.CalculateHash(-29999999, -64, -29999999);
        
        // Assert
        hash1.Should().NotBe(hash2);
        hash1.Should().BePositive();  // Long should be positive with this encoding
    }
    
    [Fact]
    public void Equals_SameCoordinates_ReturnsTrue()
    {
        // Arrange
        var node1 = new PathNode(10, 64, 20);
        var node2 = new PathNode(10, 64, 20);
        
        // Assert
        node1.Equals(node2).Should().BeTrue();
    }
    
    [Fact]
    public void Equals_DifferentCoordinates_ReturnsFalse()
    {
        // Arrange
        var node1 = new PathNode(10, 64, 20);
        var node2 = new PathNode(10, 64, 21);
        
        // Assert
        node1.Equals(node2).Should().BeFalse();
    }
    
    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        // Arrange
        var node = new PathNode(10, 64, 20);
        
        // Assert
        node.Equals(null).Should().BeFalse();
    }
    
    [Fact]
    public void Previous_CanFormChain()
    {
        // Arrange
        var start = new PathNode(0, 64, 0);
        var middle = new PathNode(1, 64, 0) { Previous = start };
        var end = new PathNode(2, 64, 0) { Previous = middle };
        
        // Assert
        end.Previous.Should().Be(middle);
        end.Previous!.Previous.Should().Be(start);
        end.Previous!.Previous!.Previous.Should().BeNull();
    }
    
    [Fact]
    public void ToString_IncludesCoordinatesAndCost()
    {
        // Arrange
        var node = new PathNode(10, 64, 20) { Cost = 123.45 };
        
        // Act
        var str = node.ToString();
        
        // Assert
        str.Should().Contain("10");
        str.Should().Contain("64");
        str.Should().Contain("20");
        str.Should().Contain("123.45");
    }
}
