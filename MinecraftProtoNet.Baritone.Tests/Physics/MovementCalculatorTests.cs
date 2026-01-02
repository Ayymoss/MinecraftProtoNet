using FluentAssertions;
using MinecraftProtoNet.Baritone.Physics;
using MinecraftProtoNet.Baritone.Tests.Infrastructure;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Physics;
using static MinecraftProtoNet.Physics.PhysicsConstants;

namespace MinecraftProtoNet.Baritone.Tests.Physics;

/// <summary>
/// Tests for MovementCalculator - pure physics calculations.
/// </summary>
public class MovementCalculatorTests
{
    #region CalculateInputAcceleration Tests
    
    [Fact]
    public void CalculateInputAcceleration_NoInput_ReturnsZero()
    {
        // Arrange & Act
        var result = MovementCalculator.CalculateInputAcceleration(0, 0, 0, 1.0f);
        
        // Assert
        result.X.Should().Be(0);
        result.Y.Should().Be(0);
        result.Z.Should().Be(0);
    }
    
    [Fact]
    public void CalculateInputAcceleration_ForwardAtYawZero_MovesToNegativeZ()
    {
        // Arrange - Yaw 0 means facing south (+Z), forward input should move to +Z
        // But in MC's coordinate system, yaw 0 = south, and forward moves in -sin direction for X, cos for Z
        var result = MovementCalculator.CalculateInputAcceleration(0, 1f, 0, 1.0f);
        
        // Assert - at yaw 0, moving forward means positive Z direction
        result.Z.Should().BeApproximately(1.0, 0.001);
        result.X.Should().BeApproximately(0, 0.001);
    }
    
    [Fact]
    public void CalculateInputAcceleration_ForwardAtYaw90_MovesToNegativeX()
    {
        // Arrange - Yaw 90 means facing west (-X)
        var result = MovementCalculator.CalculateInputAcceleration(0, 1f, 90, 1.0f);
        
        // Assert
        result.X.Should().BeApproximately(-1.0, 0.001);
        result.Z.Should().BeApproximately(0, 0.001);
    }
    
    [Fact]
    public void CalculateInputAcceleration_DiagonalInput_IsNormalized()
    {
        // Arrange - diagonal input (forward + right)
        var result = MovementCalculator.CalculateInputAcceleration(1f, 1f, 0, 1.0f);
        
        // Assert - magnitude should be 1 (normalized), not sqrt(2)
        var magnitude = Math.Sqrt(result.X * result.X + result.Z * result.Z);
        magnitude.Should().BeApproximately(1.0, 0.001);
    }
    
    [Fact]
    public void CalculateInputAcceleration_Speed_ScalesResult()
    {
        // Arrange
        var slowResult = MovementCalculator.CalculateInputAcceleration(0, 1f, 0, 0.5f);
        var fastResult = MovementCalculator.CalculateInputAcceleration(0, 1f, 0, 2.0f);
        
        // Assert
        fastResult.Z.Should().BeApproximately(slowResult.Z * 4, 0.001); // 2x speed = 4x the movement
    }
    
    #endregion
    
    #region Friction and Speed Tests
    
    [Fact]
    public void GetFrictionInfluencedSpeed_OnGround_UsesBlockFriction()
    {
        // Arrange
        const float blockFriction = 0.6f; // Default
        const float movementSpeed = 0.1f;
        
        // Act
        var speed = MovementCalculator.GetFrictionInfluencedSpeed(blockFriction, movementSpeed, onGround: true);
        
        // Assert - should be: speed * (0.216 / (0.6^3)) = 0.1 * 1.0 = 0.1
        speed.Should().BeApproximately(0.1f, 0.01f);
    }
    
    [Fact]
    public void GetFrictionInfluencedSpeed_InAir_ReturnsFlyingSpeed()
    {
        // Act
        var speed = MovementCalculator.GetFrictionInfluencedSpeed(0.6f, 0.1f, onGround: false);
        
        // Assert
        speed.Should().Be(DefaultFlyingSpeed);
    }
    
    [Fact]
    public void GetFrictionInfluencedSpeed_IceBlock_HigherSpeed()
    {
        // Arrange - ice has friction 0.98
        const float iceFriction = 0.98f;
        const float normalFriction = 0.6f;
        const float movementSpeed = 0.1f;
        
        // Act
        var iceSpeed = MovementCalculator.GetFrictionInfluencedSpeed(iceFriction, movementSpeed, onGround: true);
        var normalSpeed = MovementCalculator.GetFrictionInfluencedSpeed(normalFriction, movementSpeed, onGround: true);
        
        // Assert - ice should give lower acceleration (higher friction means less grip)
        iceSpeed.Should().BeLessThan(normalSpeed);
    }
    
    [Fact]
    public void ApplyHorizontalFriction_OnGround_ReducesVelocity()
    {
        // Arrange
        var velocity = new Vector3<double>(1.0, 0, 1.0);
        
        // Act
        var result = MovementCalculator.ApplyHorizontalFriction(velocity, 0.6f, onGround: true);
        
        // Assert - should be reduced by friction * air_drag
        result.X.Should().BeLessThan(velocity.X);
        result.Z.Should().BeLessThan(velocity.Z);
    }
    
    [Fact]
    public void ApplyHorizontalFriction_PreservesVerticalVelocity()
    {
        // Arrange
        var velocity = new Vector3<double>(1.0, 5.0, 1.0);
        
        // Act
        var result = MovementCalculator.ApplyHorizontalFriction(velocity, 0.6f, onGround: true);
        
        // Assert
        result.Y.Should().Be(velocity.Y);
    }
    
    #endregion
    
    #region Gravity and Vertical Movement Tests
    
    [Fact]
    public void ApplyGravity_ReducesVerticalVelocity()
    {
        // Arrange
        var velocity = new Vector3<double>(0, 0, 0);
        
        // Act
        var result = MovementCalculator.ApplyGravity(velocity, DefaultGravity);
        
        // Assert - should be negative (falling)
        result.Y.Should().BeLessThan(0);
    }
    
    [Fact]
    public void ApplyGravity_PreservesHorizontalVelocity()
    {
        // Arrange
        var velocity = new Vector3<double>(5.0, 0, 3.0);
        
        // Act
        var result = MovementCalculator.ApplyGravity(velocity, DefaultGravity);
        
        // Assert
        result.X.Should().Be(velocity.X);
        result.Z.Should().Be(velocity.Z);
    }
    
    [Fact]
    public void ApplyJump_SetsVerticalVelocity()
    {
        // Arrange
        var velocity = new Vector3<double>(0, 0, 0);
        
        // Act
        var result = MovementCalculator.ApplyJump(velocity, 0, isSprinting: false);
        
        // Assert
        result.Y.Should().BeApproximately(BaseJumpPower, 0.01);
    }
    
    [Fact]
    public void ApplyJump_Sprinting_AddsHorizontalBoost()
    {
        // Arrange
        var velocity = new Vector3<double>(0, 0, 0);
        
        // Act
        var normalJump = MovementCalculator.ApplyJump(velocity, 0, isSprinting: false);
        var sprintJump = MovementCalculator.ApplyJump(velocity, 0, isSprinting: true);
        
        // Assert - sprint jump should have horizontal component
        var normalHorizontal = Math.Sqrt(normalJump.X * normalJump.X + normalJump.Z * normalJump.Z);
        var sprintHorizontal = Math.Sqrt(sprintJump.X * sprintJump.X + sprintJump.Z * sprintJump.Z);
        
        sprintHorizontal.Should().BeGreaterThan(normalHorizontal);
    }
    
    [Fact]
    public void ApplyJump_HoneyBlock_ReducedJumpHeight()
    {
        // Arrange
        var velocity = new Vector3<double>(0, 0, 0);
        
        // Act
        var normalJump = MovementCalculator.ApplyJump(velocity, 0, isSprinting: false, jumpFactor: 1.0f);
        var honeyJump = MovementCalculator.ApplyJump(velocity, 0, isSprinting: false, jumpFactor: 0.5f);
        
        // Assert
        honeyJump.Y.Should().BeApproximately(normalJump.Y * 0.5, 0.01);
    }
    
    #endregion
    
    #region Climbing Tests
    
    [Fact]
    public void HandleClimbing_NotOnClimbable_ReturnsUnchanged()
    {
        // Arrange
        var velocity = new Vector3<double>(5.0, 5.0, 5.0);
        
        // Act
        var result = MovementCalculator.HandleClimbing(velocity, isOnClimbable: false, isSneaking: false);
        
        // Assert
        result.Should().Be(velocity);
    }
    
    [Fact]
    public void HandleClimbing_OnClimbable_ClampsVelocity()
    {
        // Arrange
        var velocity = new Vector3<double>(5.0, -5.0, 5.0);
        
        // Act
        var result = MovementCalculator.HandleClimbing(velocity, isOnClimbable: true, isSneaking: false);
        
        // Assert - should be clamped to ±MaxClimbSpeed
        result.X.Should().Be(MaxClimbSpeed);
        result.Z.Should().Be(MaxClimbSpeed);
        result.Y.Should().Be(-MaxClimbSpeed);
    }
    
    [Fact]
    public void HandleClimbing_SneakingOnClimbable_StopsDownwardMovement()
    {
        // Arrange
        var velocity = new Vector3<double>(0, -0.1, 0);
        
        // Act
        var result = MovementCalculator.HandleClimbing(velocity, isOnClimbable: true, isSneaking: true);
        
        // Assert
        result.Y.Should().Be(0);
    }
    
    #endregion
    
    #region Effective Speed Tests
    
    [Fact]
    public void GetEffectiveSpeed_Sprinting_Increases()
    {
        // Act
        var normal = MovementCalculator.GetEffectiveSpeed(0.1, isSprinting: false, isSneaking: false);
        var sprint = MovementCalculator.GetEffectiveSpeed(0.1, isSprinting: true, isSneaking: false);
        
        // Assert
        sprint.Should().BeGreaterThan(normal);
    }
    
    [Fact]
    public void GetEffectiveSpeed_Sneaking_Decreases()
    {
        // Act
        var normal = MovementCalculator.GetEffectiveSpeed(0.1, isSprinting: false, isSneaking: false);
        var sneak = MovementCalculator.GetEffectiveSpeed(0.1, isSprinting: false, isSneaking: true);
        
        // Assert
        sneak.Should().BeLessThan(normal);
    }
    
    #endregion
    
    #region Movement Threshold Tests
    
    [Fact]
    public void IsBelowMovementThreshold_VerySmallValue_ReturnsTrue()
    {
        // Act & Assert
        MovementCalculator.IsBelowMovementThreshold(0.001).Should().BeTrue();
        MovementCalculator.IsBelowMovementThreshold(-0.001).Should().BeTrue();
    }
    
    [Fact]
    public void IsBelowMovementThreshold_LargeValue_ReturnsFalse()
    {
        // Act & Assert
        MovementCalculator.IsBelowMovementThreshold(0.1).Should().BeFalse();
    }
    
    [Fact]
    public void ClampMinimumMovement_ZeroesSmallValues()
    {
        // Arrange
        var velocity = new Vector3<double>(0.001, 1.0, 0.001);
        
        // Act
        var result = MovementCalculator.ClampMinimumMovement(velocity);
        
        // Assert
        result.X.Should().Be(0);
        result.Y.Should().Be(1.0);
        result.Z.Should().Be(0);
    }
    
    #endregion
    
    #region Block Property Tests (with TestWorldBuilder)
    
    [Fact]
    public void GetBlockFriction_ReturnsBlockProperty()
    {
        // Arrange
        var (level, _) = TestWorldBuilder.Create()
            .WithFloor(63)
            .BuildWithPlayer();
        
        // Act
        var friction = MovementCalculator.GetBlockFriction(level, new Vector3<double>(0.5, 64, 0.5));
        
        // Assert - stone has default friction 0.6
        friction.Should().Be(DefaultBlockFriction);
    }
    
    #endregion
}
