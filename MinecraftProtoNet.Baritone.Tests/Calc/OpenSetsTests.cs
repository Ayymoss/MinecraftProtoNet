using FluentAssertions;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding.Goals;

namespace MinecraftProtoNet.Baritone.Tests.Calc;

/// <summary>
/// Tests for BinaryHeapOpenSet - Direct port from Baritone's OpenSetsTest.java
/// Source: baritone-1.21.11-REFERENCE-ONLY/src/test/java/baritone/pathing/calc/openset/OpenSetsTest.java
/// 
/// This test validates that our heap implementation behaves correctly across
/// various sizes, ensuring nodes are always returned in sorted order by cost.
/// </summary>
public class OpenSetsTests
{
    // Port of @Parameterized.Parameters - test sizes from Baritone
    public static IEnumerable<object[]> TestSizes()
    {
        // Size 1-19
        for (int size = 1; size < 20; size++)
            yield return new object[] { size };
        
        // Size 100-1000 in steps of 100
        for (int size = 100; size <= 1000; size += 100)
            yield return new object[] { size };
        
        // Larger sizes
        yield return new object[] { 5000 };
        yield return new object[] { 10000 };
    }
    
    /// <summary>
    /// Port of Baritone's testSize() test.
    /// Tests that the heap correctly maintains ordering through:
    /// 1. Insertion of random-cost nodes
    /// 2. Removal of lowest quarter (verified against sorted list)
    /// 3. Cost updates (decreasing)
    /// 4. Removal of remaining nodes
    /// Source: OpenSetsTest.java lines 76-169
    /// </summary>
    [Theory]
    [MemberData(nameof(TestSizes))]
    public void TestSize(int size)
    {
        // Port: IOpenSet[] test = new IOpenSet[]{new BinaryHeapOpenSet(), ...}
        var heap = new BinaryHeapOpenSet();
        
        // Port: assertTrue(set.isEmpty())
        heap.IsEmpty.Should().BeTrue();
        
        // Port: Generate PathNodes with random costs
        var random = new Random(42); // Fixed seed for reproducibility
        var toInsert = new PathNode[size];
        for (int i = 0; i < size; i++)
        {
            var pn = new PathNode(0, 0, 0);
            pn.CombinedCost = random.NextDouble();
            toInsert[i] = pn;
        }
        
        // Port: Create sorted copy to verify first quarter removal
        var sorted = toInsert.OrderBy(pn => pn.CombinedCost).ToList();
        var lowestQuarter = sorted.Take(size / 4).ToHashSet();
        
        // Port: Insert all nodes
        foreach (var node in toInsert)
        {
            heap.Insert(node);
        }
        
        // Port: assertFalse(set.isEmpty())
        heap.IsEmpty.Should().BeFalse();
        
        // Port: Removal round 1 - remove quarter and verify they're the lowest
        var quarterlength = size / 4;
        var firstRemovalResults = new List<double>();
        for (int j = 0; j < quarterlength; j++)
        {
            var pn = heap.RemoveLowest();
            lowestQuarter.Should().Contain(pn, 
                $"Removed node at index {j} should be in the lowest quarter");
            firstRemovalResults.Add(pn.CombinedCost);
        }
        
        // Port: Verify first removal was in ascending order
        for (int i = 0; i < firstRemovalResults.Count - 1; i++)
        {
            firstRemovalResults[i].Should().BeLessThan(firstRemovalResults[i + 1],
                $"Results at index {i} and {i+1} should be in ascending order");
        }
        
        // Port: Update costs (decrease by multiplying by random < 1)
        int cnt = 0;
        for (int i = 0; cnt < size / 2 && i < size; i++)
        {
            if (lowestQuarter.Contains(toInsert[i]))
                continue; // Already removed
            
            toInsert[i].CombinedCost *= random.NextDouble();
            heap.Update(toInsert[i]);
            cnt++;
        }
        
        // Port: Still shouldn't be empty
        heap.IsEmpty.Should().BeFalse();
        
        // Port: Removal round 2 - remove remaining 3/4
        var remainingCount = size - quarterlength;
        var secondRemovalResults = new List<double>();
        for (int j = 0; j < remainingCount; j++)
        {
            var pn = heap.RemoveLowest();
            secondRemovalResults.Add(pn.CombinedCost);
        }
        
        // Port: Verify second removal was in ascending order
        for (int i = 0; i < secondRemovalResults.Count - 1; i++)
        {
            secondRemovalResults[i].Should().BeLessThanOrEqualTo(secondRemovalResults[i + 1],
                $"Results at index {i} and {i+1} should be in ascending order");
        }
        
        // Port: assertTrue(set.isEmpty())
        heap.IsEmpty.Should().BeTrue();
    }
}
