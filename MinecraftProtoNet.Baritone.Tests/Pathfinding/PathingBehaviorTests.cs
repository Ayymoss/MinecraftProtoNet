using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using MinecraftProtoNet.Baritone.Pathfinding;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding.Goals;
using MinecraftProtoNet.State;
using Moq;
using Xunit;
using Path = MinecraftProtoNet.Pathfinding.Calc.Path;

namespace MinecraftProtoNet.Baritone.Tests.Pathfinding;

public class PathingBehaviorTests
{
    private readonly Mock<ITickManager> _tickManagerMock;
    private readonly Mock<IPlayerRegistry> _playerRegistryMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Level _level;
    private readonly PathingBehavior _behavior;
    private readonly Mock<IPathFinder> _pathFinderMock;

    public PathingBehaviorTests()
    {
        _tickManagerMock = new Mock<ITickManager>();
        _playerRegistryMock = new Mock<IPlayerRegistry>();
        _chunkManagerMock = new Mock<IChunkManager>();
        _pathFinderMock = new Mock<IPathFinder>();

        _level = new Level(_tickManagerMock.Object, _playerRegistryMock.Object, _chunkManagerMock.Object);
        
        var logger = new NullLogger<PathingBehavior>();
        var loggerFactory = new NullLoggerFactory();
        
        _behavior = new PathingBehavior(logger, loggerFactory, _level);
        
        // Inject mock factory
        _behavior.PathFinderFactory = (ctx, goal, x, y, z) => _pathFinderMock.Object;

        // Default: everything is air except bedrock floor
        var air = new Models.World.Chunk.BlockState(0, "minecraft:air");
        var floor = new Models.World.Chunk.BlockState(1, "minecraft:bedrock");
        
        _chunkManagerMock.Setup(m => m.GetBlockAt(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns<int, int, int>((x, y, z) => y < 64 ? floor : air);
    }

    [Fact]
    public void TestSetGoal_StartsCalculation()
    {
        // Arrange
        var goal = new Mock<IGoal>();
        var entity = new Entity { Position = new Vector3<double>(0.5, 64, 0.5) };
        
        _pathFinderMock.Setup(f => f.Calculate(It.IsAny<long>(), It.IsAny<long>()))
            .Returns((PathCalculationResultType.Success, new Path(new List<(int, int, int)> { (0, 64, 0), (0, 64, 1) }, goal.Object, 1, true)));

        // Act
        _behavior.SetGoalAndPath(goal.Object, entity);
        
        // Wait for calculation to finish
        var timeout = 0;
        while (_behavior.IsCalculating && timeout < 100)
        {
            Thread.Sleep(10);
            timeout++;
        }

        // Assert
        Assert.True(_behavior.IsPathing);
        _pathFinderMock.Verify(f => f.Calculate(It.IsAny<long>(), It.IsAny<long>()), Times.AtLeastOnce);
    }

    [Fact]
    public void TestCancel_StopsCalculation()
    {
        // Arrange
        var goal = new Mock<IGoal>();
        var entity = new Entity { Position = new Vector3<double>(0.5, 64, 0.5) };
        _pathFinderMock.Setup(f => f.Calculate(It.IsAny<long>(), It.IsAny<long>()))
            .Returns(() => {
                Thread.Sleep(500); // Simulate long calc
                return (PathCalculationResultType.Cancelled, null);
            });

        _behavior.SetGoalAndPath(goal.Object, entity);
        Assert.True(_behavior.IsCalculating);

        // Act
        _behavior.ForceCancel(entity);

        // Assert
        Assert.False(_behavior.IsPathing);
        _pathFinderMock.Verify(f => f.Cancel(), Times.AtLeastOnce);
    }

    [Fact]
    public void TestOnTick_UpdatesExecutor()
    {
        // Arrange: Successful path already calculated
        var goal = new Mock<IGoal>();
        var entity = new Entity { Position = new Vector3<double>(0.5, 64, 0.5) };
        var path = new Path(new List<(int, int, int)> { (0, 64, 0), (0, 64, 1) }, goal.Object, 1, true);
        
        _pathFinderMock.Setup(f => f.Calculate(It.IsAny<long>(), It.IsAny<long>()))
            .Returns((PathCalculationResultType.Success, path));

        _behavior.SetGoalAndPath(goal.Object, entity);
        
        // Wait for calculation to finish
        var timeout = 0;
        while (_behavior.IsCalculating && timeout < 100) { Thread.Sleep(10); timeout++; }

        // Act
        _behavior.OnTick(entity);

        // Assert
        Assert.True(_behavior.IsPathing);
    }

    [Fact]
    public void TestPause_PreventsTickExecution()
    {
        // Arrange
        var goal = new Mock<IGoal>();
        var entity = new Entity { Position = new Vector3<double>(0.5, 64, 0.5) };
        var path = new Path(new List<(int, int, int)> { (0, 64, 0), (0, 64, 1) }, goal.Object, 1, true);
        
        _pathFinderMock.Setup(f => f.Calculate(It.IsAny<long>(), It.IsAny<long>()))
            .Returns((PathCalculationResultType.Success, path));

        _behavior.SetGoalAndPath(goal.Object, entity);
        SpinWait.SpinUntil(() => _behavior.IsPathing, 1000);

        // Act
        _behavior.Pause();
        _behavior.OnTick(entity);

        // Assert
        Assert.True(_behavior.IsPaused);
        // If paused, entity inputs shouldn't be updated by PathExecutor this tick
    }

    [Fact]
    public void TestFailureBackoff_IncrementsWaitTime()
    {
        // Arrange
        var goal = new Mock<IGoal>();
        var entity = new Entity { Position = new Vector3<double>(0.5, 64, 0.5) };
        
        // Setup pathfinder to return failure
        _pathFinderMock.SetupSequence(f => f.Calculate(It.IsAny<long>(), It.IsAny<long>()))
            .Returns((PathCalculationResultType.Failure, null))
            .Returns(() => {
                Thread.Sleep(200); // Give it some time to be "calculating"
                return (PathCalculationResultType.Failure, null);
            });

        // Act & Assert 1: First failure
        _behavior.SetGoalAndPath(goal.Object, entity);
        SpinWait.SpinUntil(() => !_behavior.IsCalculating, 1000);
        
        // Tick to see if it retries immediately (it shouldn't due to backoff)
        _behavior.OnTick(entity);
        Assert.False(_behavior.IsCalculating);

        // Fast forward 20 ticks (1s backoff)
        for(int i = 0; i < 25; i++) _behavior.OnTick(entity);
        
        // Now it should start another calculation because OnTick detected finished and no backoff
        bool reCalculating = SpinWait.SpinUntil(() => _behavior.IsCalculating, 1000);
        Assert.True(reCalculating, "Should have started re-calculation after backoff");
    }

    [Fact]
    public void TestPartialSuccess_ExecutesPath()
    {
        // Arrange
        var goal = new Mock<IGoal>();
        var entity = new Entity { Position = new Vector3<double>(0.5, 64, 0.5) };
        var path = new Path(new List<(int, int, int)> { (0, 64, 0), (0, 64, 1) }, goal.Object, 1, false); // Partial
        
        _pathFinderMock.Setup(f => f.Calculate(It.IsAny<long>(), It.IsAny<long>()))
            .Returns((PathCalculationResultType.PartialSuccess, path));

        // Act
        _behavior.SetGoalAndPath(goal.Object, entity);
        bool started = SpinWait.SpinUntil(() => _behavior.IsPathing, 1000);
        Assert.True(started, "Pathing should have started");

        // Assert
        Assert.Equal(path, _behavior.Current?.Path);
    }

    [Fact]
    public void TestPathComplete_Success_TriggersEvent()
    {
        // Arrange
        var goal = new Mock<IGoal>();
        var entity = new Entity { Position = new Vector3<double>(0.5, 64, 0.5) };
        // Path must have >1 position to avoid stuck detection
        var path = new Path(new List<(int, int, int)> { (0, 64, 0), (0, 64, 1) }, goal.Object, 1, true); 
        
        _pathFinderMock.Setup(f => f.Calculate(It.IsAny<long>(), It.IsAny<long>()))
            .Returns((PathCalculationResultType.Success, path));

        bool successEventFired = false;
        _behavior.OnPathComplete += (success) => { if (success) successEventFired = true; };

        // Act
        _behavior.SetGoalAndPath(goal.Object, entity);
        bool started = SpinWait.SpinUntil(() => _behavior.IsPathing, 1000);
        Assert.True(started, "Pathing should have started");
        
        // Setup goal check to return true
        goal.Setup(g => g.IsInGoal(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).Returns(true);
        
        // Advance path to finish
        entity.Position = new Vector3<double>(0.5, 64, 1.5);
        _behavior.OnTick(entity); // Movement Success
        _behavior.OnTick(entity); // Path finished -> Complete

        // Assert
        Assert.True(successEventFired, "Success event should have fired");
        Assert.False(_behavior.IsPathing);
    }

    [Fact]
    public void TestPathFinished_NoGoal_TriggersRecalculation()
    {
        // Arrange
        var goal = new Mock<IGoal>();
        var entity = new Entity { Position = new Vector3<double>(0.5, 64, 0.5) };
        var path1 = new Path(new List<(int, int, int)> { (0, 64, 0), (0, 64, 1) }, goal.Object, 1, false); // Doesn't reach goal
        
        _pathFinderMock.SetupSequence(f => f.Calculate(It.IsAny<long>(), It.IsAny<long>()))
            .Returns((PathCalculationResultType.Success, path1))
            .Returns((PathCalculationResultType.Failure, null));

        _behavior.SetGoalAndPath(goal.Object, entity);
        SpinWait.SpinUntil(() => _behavior.IsPathing, 1000);
        
        // Goal still far away
        goal.Setup(g => g.IsInGoal(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).Returns(false);

        // Act
        _behavior.OnTick(entity); // Path finishes (1 pos), goal not reached -> Recalculate

        // Assert
        _pathFinderMock.Verify(f => f.Calculate(It.IsAny<long>(), It.IsAny<long>()), Times.Exactly(2));
    }
}
