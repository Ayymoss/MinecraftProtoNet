using FluentAssertions;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding.Calc;

namespace MinecraftProtoNet.Baritone.Tests.Calc;

/// <summary>
/// Tests for BinaryHeapOpenSet - priority queue for A* pathfinding.
/// </summary>
public class BinaryHeapOpenSetTests
{
    [Fact]
    public void Constructor_CreatesEmptyHeap()
    {
        // Arrange & Act
        var heap = new BinaryHeapOpenSet();
        
        // Assert
        heap.Count.Should().Be(0);
        heap.IsEmpty.Should().BeTrue();
    }
    
    [Fact]
    public void Insert_SingleNode_IncreasesCount()
    {
        // Arrange
        var heap = new BinaryHeapOpenSet();
        var node = new PathNode(0, 0, 0) { CombinedCost = 10 };
        
        // Act
        heap.Insert(node);
        
        // Assert
        heap.Count.Should().Be(1);
        heap.IsEmpty.Should().BeFalse();
    }
    
    [Fact]
    public void Insert_SetsHeapIndex()
    {
        // Arrange
        var heap = new BinaryHeapOpenSet();
        var node = new PathNode(0, 0, 0) { CombinedCost = 10 };
        
        // Act
        heap.Insert(node);
        
        // Assert
        node.HeapIndex.Should().BeGreaterThanOrEqualTo(0);
        node.IsOpen().Should().BeTrue();
    }
    
    [Fact]
    public void RemoveLowest_ReturnsLowestCostNode()
    {
        // Arrange
        var heap = new BinaryHeapOpenSet();
        var high = new PathNode(0, 0, 0) { CombinedCost = 100 };
        var low = new PathNode(1, 0, 0) { CombinedCost = 10 };
        var mid = new PathNode(2, 0, 0) { CombinedCost = 50 };
        
        heap.Insert(high);
        heap.Insert(low);
        heap.Insert(mid);
        
        // Act
        var result = heap.RemoveLowest();
        
        // Assert
        result.Should().Be(low);
    }
    
    [Fact]
    public void RemoveLowest_MarksNodeAsNotOpen()
    {
        // Arrange
        var heap = new BinaryHeapOpenSet();
        var node = new PathNode(0, 0, 0) { CombinedCost = 10 };
        heap.Insert(node);
        
        // Act
        heap.RemoveLowest();
        
        // Assert
        node.HeapIndex.Should().Be(-1);
        node.IsOpen().Should().BeFalse();
    }
    
    [Fact]
    public void RemoveLowest_EmptyHeap_Throws()
    {
        // Arrange
        var heap = new BinaryHeapOpenSet();
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => heap.RemoveLowest());
    }
    
    [Fact]
    public void PeekLowest_ReturnsLowestWithoutRemoving()
    {
        // Arrange
        var heap = new BinaryHeapOpenSet();
        var low = new PathNode(0, 0, 0) { CombinedCost = 10 };
        var high = new PathNode(1, 0, 0) { CombinedCost = 100 };
        heap.Insert(high);
        heap.Insert(low);
        
        // Act
        var result = heap.PeekLowest();
        
        // Assert
        result.Should().Be(low);
        heap.Count.Should().Be(2);  // Still in heap
    }
    
    [Fact]
    public void PeekLowest_EmptyHeap_Throws()
    {
        // Arrange
        var heap = new BinaryHeapOpenSet();
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => heap.PeekLowest());
    }
    
    [Fact]
    public void Update_DecreasedCost_BubblesUp()
    {
        // Arrange
        var heap = new BinaryHeapOpenSet();
        var node1 = new PathNode(0, 0, 0) { CombinedCost = 10 };
        var node2 = new PathNode(1, 0, 0) { CombinedCost = 100 };
        heap.Insert(node1);
        heap.Insert(node2);
        
        // Act - decrease node2's cost to be lower than node1
        node2.CombinedCost = 5;
        heap.Update(node2);
        
        // Assert
        heap.PeekLowest().Should().Be(node2);
    }
    
    [Fact]
    public void Clear_RemovesAllNodes()
    {
        // Arrange
        var heap = new BinaryHeapOpenSet();
        var node1 = new PathNode(0, 0, 0) { CombinedCost = 10 };
        var node2 = new PathNode(1, 0, 0) { CombinedCost = 20 };
        heap.Insert(node1);
        heap.Insert(node2);
        
        // Act
        heap.Clear();
        
        // Assert
        heap.Count.Should().Be(0);
        heap.IsEmpty.Should().BeTrue();
        node1.HeapIndex.Should().Be(-1);
        node2.HeapIndex.Should().Be(-1);
    }
    
    [Fact]
    public void Insert_ManyNodes_MaintainsHeapProperty()
    {
        // Arrange
        var heap = new BinaryHeapOpenSet();
        var random = new Random(42);  // Fixed seed for reproducibility
        var nodes = Enumerable.Range(0, 100)
            .Select(i => new PathNode(i, 0, 0) { CombinedCost = random.NextDouble() * 1000 })
            .ToList();
        
        // Act
        foreach (var node in nodes)
        {
            heap.Insert(node);
        }
        
        // Assert - removeLowest should always return nodes in sorted order
        var lastCost = double.MinValue;
        while (!heap.IsEmpty)
        {
            var node = heap.RemoveLowest();
            node.CombinedCost.Should().BeGreaterThanOrEqualTo(lastCost);
            lastCost = node.CombinedCost;
        }
    }
    
    [Fact]
    public void Insert_GrowsCapacity()
    {
        // Arrange - start with small capacity
        var heap = new BinaryHeapOpenSet(4);
        
        // Act - insert more than initial capacity
        for (int i = 0; i < 100; i++)
        {
            heap.Insert(new PathNode(i, 0, 0) { CombinedCost = i });
        }
        
        // Assert
        heap.Count.Should().Be(100);
    }
}
