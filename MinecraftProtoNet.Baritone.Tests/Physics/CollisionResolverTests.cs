using FluentAssertions;
using MinecraftProtoNet.Baritone.Physics;
using MinecraftProtoNet.Baritone.Tests.Infrastructure;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Physics;
using MinecraftProtoNet.Physics.Shapes;

namespace MinecraftProtoNet.Baritone.Tests.Physics;

/// <summary>
/// Tests for CollisionResolver - collision detection and resolution.
/// Uses TestWorldBuilder for deterministic block configurations.
/// </summary>
public class CollisionResolverTests
{
    #region Basic Movement (No Collision)
    
    [Fact]
    public void MoveWithCollisions_NoBlocks_MovesFullDelta()
    {
        // Arrange - empty world
        var level = TestWorldBuilder.Create().Build();
        var boundingBox = new AABB(0, 64, 0, 0.6, 65.8, 0.6); // Player at (0, 64, 0)
        var delta = new Vector3<double>(1.0, 0, 0);
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(boundingBox, level, delta, wasOnGround: true, isSneaking: false);
        
        // Assert
        result.ActualDelta.X.Should().BeApproximately(1.0, 0.001);
        result.CollidedX.Should().BeFalse();
        result.CollidedY.Should().BeFalse();
        result.CollidedZ.Should().BeFalse();
    }
    
    [Fact]
    public void MoveWithCollisions_ZeroDelta_ReturnsZero()
    {
        // Arrange
        var level = TestWorldBuilder.Create().WithFloor().Build();
        var boundingBox = new AABB(0, 64, 0, 0.6, 65.8, 0.6);
        var delta = Vector3<double>.Zero;
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(boundingBox, level, delta, wasOnGround: true, isSneaking: false);
        
        // Assert
        result.ActualDelta.Should().Be(Vector3<double>.Zero);
    }
    
    #endregion
    
    #region Floor Collision
    
    [Fact]
    public void MoveWithCollisions_FallingOntoFloor_StopsAtFloor()
    {
        // Arrange - floor at Y=63, player at Y=64
        var level = TestWorldBuilder.Create()
            .WithFloor(63)
            .Build();
        
        // Player just above floor
        var boundingBox = new AABB(0, 64.01, 0, 0.6, 65.81, 0.6);
        var delta = new Vector3<double>(0, -1.0, 0); // Falling
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(boundingBox, level, delta, wasOnGround: false, isSneaking: false);
        
        // Assert
        result.CollidedY.Should().BeTrue();
        result.LandedOnGround.Should().BeTrue();
        result.FinalBoundingBox.Min.Y.Should().BeApproximately(64.0, 0.01, "Should stop at floor surface");
    }
    
    [Fact]
    public void MoveWithCollisions_OnFloor_NoVerticalMovement()
    {
        // Arrange - player standing on floor
        var level = TestWorldBuilder.Create()
            .WithFloor(63)
            .Build();
        
        var boundingBox = new AABB(0, 64, 0, 0.6, 65.8, 0.6);
        var delta = new Vector3<double>(0, -0.0784, 0); // Gravity tick
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(boundingBox, level, delta, wasOnGround: true, isSneaking: false);
        
        // Assert
        result.CollidedY.Should().BeTrue();
        result.ActualDelta.Y.Should().BeApproximately(0, 0.01);
    }
    
    #endregion
    
    #region Wall Collision
    
    [Fact]
    public void MoveWithCollisions_WalkingIntoWall_StopsAtWall()
    {
        // Arrange - wall at X=2
        var level = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithWall(2, 64, 66, -1, 1) // Wall blocking X direction
            .Build();
        
        var boundingBox = new AABB(0, 64, 0, 0.6, 65.8, 0.6);
        var delta = new Vector3<double>(3.0, 0, 0); // Try to move through wall
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(boundingBox, level, delta, wasOnGround: true, isSneaking: false);
        
        // Assert
        result.CollidedX.Should().BeTrue();
        result.ActualDelta.X.Should().BeLessThan(3.0);
        result.FinalBoundingBox.Max.X.Should().BeLessThanOrEqualTo(2.0, "Should stop before wall");
    }
    
    [Fact]
    public void MoveWithCollisions_WalkingParallelToWall_NoCollision()
    {
        // Arrange - wall at X=2
        var level = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithWall(2, 64, 66, -10, 10)
            .Build();
        
        var boundingBox = new AABB(0, 64, 0, 0.6, 65.8, 0.6);
        var delta = new Vector3<double>(0, 0, 1.0); // Moving parallel to wall
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(boundingBox, level, delta, wasOnGround: true, isSneaking: false);
        
        // Assert
        result.CollidedX.Should().BeFalse();
        result.CollidedZ.Should().BeFalse();
        result.ActualDelta.Z.Should().BeApproximately(1.0, 0.001);
    }
    
    #endregion
    
    #region Ceiling Collision
    
    [Fact]
    public void MoveWithCollisions_JumpingIntoCeiling_StopsAtCeiling()
    {
        // Arrange - ceiling at Y=67 (player height is 1.8, so ceiling at 67 means headroom of ~1 block)
        var level = TestWorldBuilder.Create()
            .WithFloor(63)
            .Build();
        level.GetBlockAt(0, 67, 0); // Ensure chunk exists
        // Use TestWorldBuilder properly
        var (level2, _) = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithBlock(0, 67, 0, "minecraft:stone") // Ceiling block
            .WithBlock(1, 67, 0, "minecraft:stone")
            .WithBlock(0, 67, 1, "minecraft:stone")
            .BuildWithPlayer();
        
        var boundingBox = new AABB(0, 64, 0, 0.6, 65.8, 0.6);
        var delta = new Vector3<double>(0, 2.0, 0); // Jumping up
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(boundingBox, level2, delta, wasOnGround: true, isSneaking: false);
        
        // Assert
        result.CollidedY.Should().BeTrue();
        result.ActualDelta.Y.Should().BeLessThan(2.0);
    }
    
    #endregion
    
    #region Step-Up
    
    [Fact]
    public void MoveWithCollisions_WalkingIntoHalfBlock_StepsUp()
    {
        // Arrange - half-block step at X=1
        var (level, _) = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithBlock(1, 64, 0, "minecraft:stone") // Step block
            .BuildWithPlayer();
        
        var boundingBox = new AABB(0, 64, -0.3, 0.6, 65.8, 0.3);
        var delta = new Vector3<double>(1.5, 0, 0); // Walking towards step
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(boundingBox, level, delta, wasOnGround: true, isSneaking: false);
        
        // Assert - should step up onto the block
        ((double)result.FinalBoundingBox.Min.Y).Should().BeApproximately(65.0, 0.1, "Should step up onto the block");
    }
    
    [Fact]
    public void MoveWithCollisions_Sneaking_NoStepUp()
    {
        // Arrange - step block at X=1
        var (level, _) = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithBlock(1, 64, 0, "minecraft:stone")
            .BuildWithPlayer();
        
        var boundingBox = new AABB(0, 64, -0.3, 0.6, 65.8, 0.3);
        var delta = new Vector3<double>(1.5, 0, 0);
        
        // Act - sneaking prevents step-up
        var result = CollisionResolver.MoveWithCollisions(boundingBox, level, delta, wasOnGround: true, isSneaking: true);
        
        // Assert - should NOT step up when sneaking
        result.CollidedX.Should().BeTrue();
        ((double)result.FinalBoundingBox.Min.Y).Should().BeApproximately(64.0, 0.1);
    }
    
    #endregion
    
    #region HasAnyCollision Tests
    
    [Fact]
    public void HasAnyCollision_EmptySpace_ReturnsFalse()
    {
        // Arrange
        var level = TestWorldBuilder.Create().Build();
        var box = new AABB(0, 100, 0, 1, 101, 1);
        
        // Act & Assert
        CollisionResolver.HasAnyCollision(box, level).Should().BeFalse();
    }
    
    [Fact]
    public void HasAnyCollision_IntersectsBlock_ReturnsTrue()
    {
        // Arrange
        var (level, _) = TestWorldBuilder.Create()
            .WithBlock(0, 64, 0, "minecraft:stone")
            .BuildWithPlayer();
        
        var box = new AABB(-0.5, 63.5, -0.5, 0.5, 64.5, 0.5);
        
        // Act & Assert
        CollisionResolver.HasAnyCollision(box, level).Should().BeTrue();
    }
    
    #endregion
    
    #region Diagonal Movement
    
    [Fact]
    public void MoveWithCollisions_DiagonalMovement_ResolvesCorrectly()
    {
        // Arrange - corner with walls
        var (level, _) = TestWorldBuilder.Create()
            .WithFloor(63)
            .BuildWithPlayer();
        
        var boundingBox = new AABB(0, 64, 0, 0.6, 65.8, 0.6);
        var delta = new Vector3<double>(1.0, 0, 1.0); // Diagonal movement
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(boundingBox, level, delta, wasOnGround: true, isSneaking: false);
        
        // Assert - should move full distance diagonally
        result.ActualDelta.X.Should().BeApproximately(1.0, 0.001);
        result.ActualDelta.Z.Should().BeApproximately(1.0, 0.001);
    }
    
    #endregion
    
    #region Complex Shapes (Slabs & Stairs)

    [Fact]
    public void MoveWithCollisions_WalkingOnSlab_MaintainsHeight()
    {
        // Arrange
        // Bottom slab at (1, 63, 0)
        var (level, player) = TestWorldBuilder.Create()
            .WithFloor(62)
            .WithBlock(1, 63, 0, "minecraft:stone_slab", properties: new() { ["type"] = "bottom" })
            .WithPlayer(0.5, 63.0, 0.5) // Player standing on floor
            .BuildWithPlayer();
        
        var delta = new Vector3<double>(1.0, 0, 0); // Try to walk onto slab
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(player.GetBoundingBox(), level, delta, wasOnGround: true, isSneaking: false);
        
        // Assert
        // Should have stepped up to 63.5 (bottom slab height is 0.5)
        result.FinalBoundingBox.MinY.Should().BeApproximately(63.5, 0.001);
        result.FinalBoundingBox.MinX.Should().BeApproximately(1.2, 0.001); // Center at 1.5 - halfWidth(0.3)
    }

    [Fact]
    public void MoveWithCollisions_WalkingUnderUpperSlab_MaintainsHeadroom()
    {
        // Arrange
        // Upper slab at (1, 65, 0) - this leaves 1.5 blocks of space (63 to 65.5)
        // Wait, player height is 1.8. 1.5 blocks is NOT enough.
        // Let's use slab at 66.
        var (level, player) = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithBlock(1, 65, 0, "minecraft:stone_slab", properties: new() { ["type"] = "top" })
            .WithPlayer(0.5, 64.0, 0.5)
            .BuildWithPlayer();
        
        var delta = new Vector3<double>(1.0, 0, 0);
        
        // Act
        // Distance from floor (64) to bottom of top slab (65.5) is 1.5.
        // Player height is 1.8. Collision should occur.
        var result = CollisionResolver.MoveWithCollisions(player.GetBoundingBox(), level, delta, wasOnGround: true, isSneaking: false);
        
        // Assert
        result.FinalBoundingBox.MinX.Should().BeLessThan(1.0); // Blocked
    }

    [Fact]
    public void MoveWithCollisions_StepUpStairs_Works()
    {
        // Arrange
        var (level, player) = TestWorldBuilder.Create()
            .WithFloor(63)
            .WithBlock(1, 64, 0, "minecraft:oak_stairs", properties: new() { ["half"] = "bottom" })
            .WithPlayer(0.5, 64.0, 0.5)
            .BuildWithPlayer();
        
        var delta = new Vector3<double>(1.0, 0, 0);
        
        // Act
        var result = CollisionResolver.MoveWithCollisions(player.GetBoundingBox(), level, delta, wasOnGround: true, isSneaking: false);
        
        // Assert
        // Should step up half a block (0.5)
        result.FinalBoundingBox.MinY.Should().BeApproximately(64.5, 0.001);
        result.FinalBoundingBox.MinX.Should().BeApproximately(1.2, 0.001); // Center at 1.5 - halfWidth(0.3)
    }

    #endregion
}
