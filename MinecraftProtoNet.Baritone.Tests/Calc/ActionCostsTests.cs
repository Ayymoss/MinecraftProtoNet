using Xunit;
using FluentAssertions;
using MinecraftProtoNet.Pathfinding;

namespace MinecraftProtoNet.Baritone.Tests.Calc;

/// <summary>
/// Tests for ActionCosts - Direct port from Baritone's ActionCostsTest.java
/// Source: baritone-1.21.11-REFERENCE-ONLY/src/test/java/baritone/pathing/movement/ActionCostsTest.java
/// 
/// These tests ensure our cost calculations match Baritone's exactly.
/// </summary>
public class ActionCostsTests
{
    /// <summary>
    /// Port of Baritone's testFallNBlocksCost() test.
    /// Verifies that:
    /// 1. FALL_N_BLOCKS_COST array has correct length (4097 elements for 0-4096 blocks)
    /// 2. Each cost converts back to the correct number of blocks fallen
    /// 3. Specific cost constants match expected values
    /// </summary>
    [Fact]
    public void TestFallNBlocksCost()
    {
        // Port: assertEquals(FALL_N_BLOCKS_COST.length, 4097)
        ActionCosts.FallNBlocksCost.Length.Should().Be(4097, 
            "Fall 0 blocks through fall 4096 blocks");
        
        // Port: for (int i = 0; i < 4097; i++) { double blocks = ticksToBlocks(FALL_N_BLOCKS_COST[i]); assertEquals(blocks, i, 0.00000000001); }
        for (int i = 0; i < 4097; i++)
        {
            double blocks = TicksToBlocks(ActionCosts.FallNBlocksCost[i]);
            blocks.Should().BeApproximately(i, 0.00000000001, 
                $"Cost at index {i} should convert back to {i} blocks fallen");
        }
        
        // Port: assertEquals(FALL_1_25_BLOCKS_COST, 6.2344, 0.00001)
        ActionCosts.Fall125BlocksCost.Should().BeApproximately(6.2344, 0.00001,
            "1.25 block fall cost should match Baritone value");
        
        // Port: assertEquals(FALL_0_25_BLOCKS_COST, 3.0710, 0.00001)
        ActionCosts.Fall025BlocksCost.Should().BeApproximately(3.0710, 0.00001,
            "0.25 block fall cost should match Baritone value");
        
        // Port: assertEquals(JUMP_ONE_BLOCK_COST, 3.1634, 0.00001)
        ActionCosts.JumpOneBlockCost.Should().BeApproximately(3.1634, 0.00001,
            "Jump one block cost should match Baritone value");
    }
    
    /// <summary>
    /// Port of Baritone's ticksToBlocks() helper method.
    /// Converts a tick cost back to the distance fallen.
    /// Source: ActionCostsTest.java lines 39-50
    /// </summary>
    private double TicksToBlocks(double ticks)
    {
        double fallDistance = 0;
        int integralComponent = (int)Math.Floor(ticks);
        
        for (int tick = 0; tick < integralComponent; tick++)
        {
            fallDistance += ActionCosts.Velocity(tick);
        }
        
        double partialTickComponent = ticks - Math.Floor(ticks);
        double finalPartialTickVelocity = ActionCosts.Velocity(integralComponent);
        double finalPartialTickDistance = finalPartialTickVelocity * partialTickComponent;
        fallDistance += finalPartialTickDistance;
        
        return fallDistance;
    }
}
