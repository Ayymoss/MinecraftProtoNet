using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using Xunit;

namespace MinecraftProtoNet.Baritone.Tests.Goals;

/// <summary>
/// Tests for new goal types: GoalComposite, GoalGetToBlock, GoalInverted, GoalTwoBlocks.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/
/// </summary>
public class NewGoalTests
{
    // ===== GoalComposite Tests =====

    [Fact]
    public void GoalComposite_IsInGoal_AnySubgoal_ReturnsTrue()
    {
        var goal1 = new GoalBlock(0, 64, 0);
        var goal2 = new GoalBlock(10, 64, 10);
        var composite = new GoalComposite(goal1, goal2);

        Assert.True(composite.IsInGoal(0, 64, 0));   // Matches goal1
        Assert.True(composite.IsInGoal(10, 64, 10)); // Matches goal2
        Assert.False(composite.IsInGoal(5, 64, 5));  // Matches neither
    }

    [Fact]
    public void GoalComposite_Heuristic_ReturnsMinOfSubgoals()
    {
        var goal1 = new GoalBlock(0, 64, 0);
        var goal2 = new GoalBlock(100, 64, 100);
        var composite = new GoalComposite(goal1, goal2);

        // From (1, 64, 0), goal1 is closer
        var heuristic1 = composite.Heuristic(1, 64, 0);
        var heuristicGoal1 = goal1.Heuristic(1, 64, 0);
        var heuristicGoal2 = goal2.Heuristic(1, 64, 0);

        Assert.Equal(Math.Min(heuristicGoal1, heuristicGoal2), heuristic1);
        Assert.Equal(heuristicGoal1, heuristic1); // goal1 should be closer
    }

    [Fact]
    public void GoalComposite_Empty_NeverInGoal()
    {
        var composite = new GoalComposite();
        Assert.False(composite.IsInGoal(0, 0, 0));
        Assert.Equal(double.MaxValue, composite.Heuristic(0, 0, 0));
    }

    // ===== GoalGetToBlock Tests =====

    [Fact]
    public void GoalGetToBlock_IsInGoal_AdjacentPositions_ReturnsTrue()
    {
        var goal = new GoalGetToBlock(5, 64, 5);

        // Adjacent positions (manhattan distance = 1)
        Assert.True(goal.IsInGoal(5, 64, 6));   // +Z
        Assert.True(goal.IsInGoal(5, 64, 4));   // -Z
        Assert.True(goal.IsInGoal(6, 64, 5));   // +X
        Assert.True(goal.IsInGoal(4, 64, 5));   // -X
        Assert.True(goal.IsInGoal(5, 65, 5));   // +Y
        Assert.True(goal.IsInGoal(5, 63, 5));   // -Y (with adjustment)
    }

    [Fact]
    public void GoalGetToBlock_IsInGoal_AtBlock_ReturnsTrue()
    {
        var goal = new GoalGetToBlock(5, 64, 5);
        
        // Baritone allows manhattan distance <= 1, so 0 is valid
        // This matches the formula: abs(0) + abs(0) + abs(0) = 0 <= 1
        Assert.True(goal.IsInGoal(5, 64, 5));
    }

    [Fact]
    public void GoalGetToBlock_IsInGoal_DiagonalPosition_ReturnsFalse()
    {
        var goal = new GoalGetToBlock(5, 64, 5);
        
        // Diagonal positions have manhattan distance > 1
        Assert.False(goal.IsInGoal(6, 64, 6));
        Assert.False(goal.IsInGoal(4, 64, 4));
    }

    // ===== GoalInverted Tests =====

    [Fact]
    public void GoalInverted_IsInGoal_AlwaysFalse()
    {
        var original = new GoalBlock(0, 64, 0);
        var inverted = new GoalInverted(original);

        Assert.False(inverted.IsInGoal(0, 64, 0)); // At original goal
        Assert.False(inverted.IsInGoal(100, 64, 100)); // Far away
    }

    [Fact]
    public void GoalInverted_Heuristic_NegatesOriginal()
    {
        var original = new GoalBlock(0, 64, 0);
        var inverted = new GoalInverted(original);

        var originalHeuristic = original.Heuristic(10, 64, 0);
        var invertedHeuristic = inverted.Heuristic(10, 64, 0);

        Assert.Equal(-originalHeuristic, invertedHeuristic);
    }

    [Fact]
    public void GoalInverted_Heuristic_FavorsFarPositions()
    {
        var original = new GoalBlock(0, 64, 0);
        var inverted = new GoalInverted(original);

        // Closer to original goal = lower original heuristic = HIGHER inverted heuristic (worse)
        var nearHeuristic = inverted.Heuristic(1, 64, 0);
        var farHeuristic = inverted.Heuristic(100, 64, 0);

        Assert.True(farHeuristic < nearHeuristic, "Far position should have lower (better) heuristic");
    }

    // ===== GoalTwoBlocks Tests =====

    [Fact]
    public void GoalTwoBlocks_IsInGoal_AtBlock_ReturnsTrue()
    {
        var goal = new GoalTwoBlocks(5, 64, 5);
        Assert.True(goal.IsInGoal(5, 64, 5)); // At block
    }

    [Fact]
    public void GoalTwoBlocks_IsInGoal_OneBelowBlock_ReturnsTrue()
    {
        var goal = new GoalTwoBlocks(5, 64, 5);
        Assert.True(goal.IsInGoal(5, 63, 5)); // One below
    }

    [Fact]
    public void GoalTwoBlocks_IsInGoal_TwoBelow_ReturnsFalse()
    {
        var goal = new GoalTwoBlocks(5, 64, 5);
        Assert.False(goal.IsInGoal(5, 62, 5)); // Two below
    }

    [Fact]
    public void GoalTwoBlocks_IsInGoal_AboveBlock_ReturnsFalse()
    {
        var goal = new GoalTwoBlocks(5, 64, 5);
        Assert.False(goal.IsInGoal(5, 65, 5)); // Above
    }

    [Fact]
    public void GoalTwoBlocks_IsInGoal_WrongX_ReturnsFalse()
    {
        var goal = new GoalTwoBlocks(5, 64, 5);
        Assert.False(goal.IsInGoal(6, 64, 5)); // Wrong X
        Assert.False(goal.IsInGoal(6, 63, 5)); // Wrong X, one below
    }
}
