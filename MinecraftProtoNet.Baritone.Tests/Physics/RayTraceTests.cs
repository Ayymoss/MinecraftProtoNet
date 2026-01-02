using FluentAssertions;
using MinecraftProtoNet.Baritone.Tests.Infrastructure;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;
using System.Collections.Generic;
using MinecraftProtoNet.Enums;
using Xunit;

namespace MinecraftProtoNet.Baritone.Tests.Physics;

public class RayTraceTests
{
    [Fact]
    public void RayTrace_ClearPath_ReturnsNull()
    {
        // Arrange
        var level = TestWorldBuilder.Create()
            .Build();
        
        var start = new Vector3<double>(0.5, 65, 0.5);
        var direction = new Vector3<double>(5, 0, 0);
        
        // Act
        var hit = level.RayCast(start, direction, 10.0);
        
        // Assert
        hit.Should().BeNull();
    }

    [Fact]
    public void RayTrace_BlockedByFullBlock_ReturnsHit()
    {
        // Arrange
        var level = TestWorldBuilder.Create()
            .WithBlock(5, 65, 0, "minecraft:stone")
            .Build();
        
        var start = new Vector3<double>(0.5, 65.5, 0.5);
        var direction = new Vector3<double>(1, 0, 0);
        
        // Act
        var hit = level.RayCast(start, direction, 10.0);
        
        // Assert
        hit.Should().NotBeNull();
        hit.BlockPosition.X.Should().Be(5);
        hit.BlockPosition.Y.Should().Be(65);
        hit.BlockPosition.Z.Should().Be(0);
        hit.Face.Should().Be(BlockFace.West);
        hit.Block.Should().NotBeNull();
        hit.Block.Name.Should().Be("minecraft:stone");
    }

    [Fact]
    public void RayTrace_BlockedBySlab_ReturnsHit()
    {
        // Arrange
        // Bottom slab 0.5 high. Ray at 64.25 should hit.
        var level = TestWorldBuilder.Create()
            .WithBlock(5, 64, 0, "minecraft:stone_slab", properties: new() { ["type"] = "bottom" })
            .Build();
        
        var start = new Vector3<double>(0.5, 64.25, 0.5);
        var direction = new Vector3<double>(1, 0, 0);
        
        // Act
        var hit = level.RayCast(start, direction, 10.0);
        
        // Assert
        hit.Should().NotBeNull();
        hit!.BlockPosition.X.Should().Be(5);
    }

    [Fact]
    public void RayTrace_ShootThroughGap_ReturnsNull()
    {
        // Arrange
        // Ray at 64.75 should pass OVER bottom slab (0.5 high)
        var level = TestWorldBuilder.Create()
            .WithBlock(5, 64, 0, "minecraft:stone_slab", properties: new() { ["type"] = "bottom" })
            .Build();
        
        var start = new Vector3<double>(0.5, 64.75, 0.5);
        var direction = new Vector3<double>(1, 0, 0);
        
        // Act
        var hit = level.RayCast(start, direction, 10.0);
        
        // Assert
        hit.Should().BeNull();
    }
}
