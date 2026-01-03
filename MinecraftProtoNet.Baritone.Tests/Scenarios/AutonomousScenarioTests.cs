using FluentAssertions;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Tests.Infrastructure;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using Xunit;
using Xunit.Abstractions;

namespace MinecraftProtoNet.Baritone.Tests.Scenarios;

/// <summary>
/// Integration tests for autonomous pathfinding scenarios.
/// These tests simulate complete navigation tasks in mocked worlds.
/// </summary>
public class AutonomousScenarioTests(ITestOutputHelper output)
{
    /// <summary>
    /// Test: Simple flat walk from origin to a nearby goal.
    /// </summary>
    [Fact]
    public void SimpleWalk_FlatGround_ReachesGoal()
    {
        // Arrange: Flat floor, player at origin, goal 5 blocks away
        var world = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithPlayer(0.5, 64, 0.5);

        var runner = new MockedWorldRunner(world);
        var goal = new GoalBlock(5, 64, 0);

        // Act
        var result = runner.RunToGoal(goal);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}");
        result.Success.Should().BeTrue();
        result.TicksUsed.Should().BeLessThan(200);
    }

    /// <summary>
    /// Test: Walk up stairs (ascending movement).
    /// </summary>
    [Fact]
    public void Ascend_Stairs_ReachesTopPlatform()
    {
        // Arrange: Floor with 4-step staircase going +X
        var world = TestWorldBuilder.Create()
            .WithFloor(63, halfWidth: 20) // Larger floor
            .WithPlayer(0.5, 64, 0.5);

        // Build stairs: each step at Y=64, 65, 66, 67
        world.WithBlock(1, 64, 0, "minecraft:stone")
             .WithBlock(2, 65, 0, "minecraft:stone")
             .WithBlock(3, 66, 0, "minecraft:stone")
             .WithBlock(4, 67, 0, "minecraft:stone");

        var runner = new MockedWorldRunner(world);
        var goal = new GoalBlock(4, 68, 0); // On top of last step

        runner.OnTick += (tick, entity) =>
        {
            if (tick % 20 == 0)
                output.WriteLine($"Tick {tick}: ({entity.Position.X:F1}, {entity.Position.Y:F1}, {entity.Position.Z:F1}) OnGround={entity.IsOnGround}");
        };

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 500);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}, Final: {result.FinalPosition}");
        result.Success.Should().BeTrue();
    }

    /// <summary>
    /// Test: Descend from a platform (1-block step down).
    /// </summary>
    [Fact]
    public void Descend_FromPlatform_ReachesGround()
    {
        // Arrange: Simple 1-block platform, goal on ground nearby
        var world = TestWorldBuilder.Create()
            .WithFloor(63, halfWidth: 20)
            .WithBlock(0, 64, 0, "minecraft:stone") // 1-block platform
            .WithPlayer(0.5, 65, 0.5);

        var runner = new MockedWorldRunner(world);
        var goal = new GoalBlock(3, 64, 3); // Goal on ground nearby

        runner.OnTick += (tick, entity) =>
        {
            if (tick % 20 == 0)
                output.WriteLine($"Tick {tick}: ({entity.Position.X:F1}, {entity.Position.Y:F1}, {entity.Position.Z:F1}) OnGround={entity.IsOnGround}");
        };

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 300);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}");
        result.Success.Should().BeTrue();
    }

    /// <summary>
    /// Test: Navigate around an obstacle.
    /// </summary>
    [Fact]
    public void Navigate_AroundObstacle_ReachesGoal()
    {
        // Arrange: Floor with wall blocking direct path
        var world = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithWall(3, 64, 67, -3, 3) // Wall at X=3 from Z=-3 to Z=3
            .WithPlayer(0.5, 64, 0.5);

        var runner = new MockedWorldRunner(world);
        var goal = new GoalBlock(6, 64, 0); // Goal is past the wall

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 800);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}");
        result.Success.Should().BeTrue();
    }

    /// <summary>
    /// Test: Multi-checkpoint navigation.
    /// </summary>
    [Fact]
    public void Checkpoints_MultipleGoals_AllReached()
    {
        // Arrange: Simple floor with multiple checkpoints
        var world = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithPlayer(0.5, 64, 0.5);

        var runner = new MockedWorldRunner(world);
        var checkpointRunner = new CheckpointRunner(runner);

        var checkpoints = CheckpointRunner.CreateCheckpoints(
            (3, 64, 0),
            (3, 64, 3),
            (0, 64, 3),
            (0, 64, 0)
        );

        checkpointRunner.OnCheckpointReached += (i, ticks) =>
            output.WriteLine($"Checkpoint {i} reached in {ticks} ticks");
        checkpointRunner.OnCheckpointFailed += (i, reason) =>
            output.WriteLine($"Checkpoint {i} FAILED: {reason}");

        // Act
        var result = checkpointRunner.RunCheckpoints(checkpoints);

        // Assert
        output.WriteLine($"All reached: {result.AllReached}, Total ticks: {result.TotalTicks}");
        result.AllReached.Should().BeTrue();
        result.TicksPerCheckpoint.Should().HaveCount(4);
    }

    /// <summary>
    /// Test: Jump over a 2-block gap.
    /// </summary>
    [Fact]
    public void ParkourJump_TwoBlockGap_Crosses()
    {
        // Arrange: Floor with 2-block gap
        var world = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithGap(2, 63, 0, 2, 1, 0) // 2-block gap at X=2,3
            .WithPlayer(0.5, 64, 0.5);

        var runner = new MockedWorldRunner(world);
        var goal = new GoalBlock(5, 64, 0);

        runner.OnTick += (tick, entity) =>
        {
            if (tick % 10 == 0)
                output.WriteLine($"Tick {tick}: ({entity.Position.X:F2}, {entity.Position.Y:F2}, {entity.Position.Z:F2}) OnGround={entity.IsOnGround}");
        };

        // Act
        var result = runner.RunToGoal(goal, maxTicks: 500);

        // Assert
        output.WriteLine($"Result: {result.Message}, Ticks: {result.TicksUsed}");
        result.Success.Should().BeTrue("the bot should be able to parkour jump over a 2-block gap");
    }
}
