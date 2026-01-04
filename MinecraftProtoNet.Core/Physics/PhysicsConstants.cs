namespace MinecraftProtoNet.Core.Physics;

/// <summary>
/// Physics constants matching Minecraft Java 26.1.
/// All values sourced from decompiled net.minecraft.world.entity.LivingEntity and related classes.
/// </summary>
public static class PhysicsConstants
{
    // ===== Gravity =====
    
    /// <summary>
    /// Default gravity acceleration per tick (blocks/tick²).
    /// Source: LivingEntity.DEFAULT_BASE_GRAVITY
    /// </summary>
    public const double DefaultGravity = 0.08;

    /// <summary>
    /// Gravity when Slow Falling effect is active.
    /// Source: LivingEntity.getEffectiveGravity()
    /// </summary>
    public const double SlowFallingGravity = 0.01;

    // ===== Jump =====
    
    /// <summary>
    /// Base vertical velocity when jumping (blocks/tick).
    /// Source: LivingEntity.BASE_JUMP_POWER = 0.42F
    /// </summary>
    public const float BaseJumpPower = 0.42f;

    /// <summary>
    /// Forward boost applied when sprint-jumping.
    /// Source: LivingEntity.jumpFromGround() uses 0.2
    /// </summary>
    public const double SprintJumpBoost = 0.2;

    // ===== Friction and Drag =====
    
    /// <summary>
    /// Input friction applied to movement input.
    /// Source: LivingEntity.INPUT_FRICTION = 0.98F
    /// </summary>
    public const float InputFriction = 0.98f;

    /// <summary>
    /// Base air drag multiplier applied each tick.
    /// Source: travelInAir() uses friction * 0.91F where base friction is 1.0 in air
    /// </summary>
    public const float AirDrag = 0.91f;

    /// <summary>
    /// Vertical drag applied in air (for flying animals it's same as horizontal).
    /// Source: travelInAir() uses 0.98F for vertical
    /// </summary>
    public const float VerticalAirDrag = 0.98f;

    /// <summary>
    /// Default block friction (slipperiness).
    /// Most blocks use this value. Ice is higher, soul sand is lower.
    /// Source: Block.getFriction() default
    /// </summary>
    public const float DefaultBlockFriction = 0.6f;

    /// <summary>
    /// Ice block friction (more slippery).
    /// </summary>
    public const float IceFriction = 0.98f;

    /// <summary>
    /// Packed ice friction.
    /// </summary>
    public const float PackedIceFriction = 0.98f;

    /// <summary>
    /// Blue ice friction (most slippery).
    /// </summary>
    public const float BlueIceFriction = 0.989f;

    /// <summary>
    /// Slime block friction.
    /// </summary>
    public const float SlimeFriction = 0.8f;

    /// <summary>
    /// Honey block friction.
    /// </summary>
    public const float HoneyFriction = 0.4f;

    // ===== Movement Speed =====
    
    /// <summary>
    /// Base player movement speed attribute value.
    /// Source: Player default MOVEMENT_SPEED attribute
    /// </summary>
    public const double BaseMovementSpeed = 0.1;

    /// <summary>
    /// Sprint speed modifier (+30%).
    /// Source: SPEED_MODIFIER_SPRINTING in LivingEntity
    /// </summary>
    public const double SprintSpeedModifier = 0.3;

    /// <summary>
    /// Sneaking speed multiplier from attribute.
    /// Source: Attributes.SNEAKING_SPEED default
    /// </summary>
    public const double SneakingSpeedMultiplier = 0.3;

    /// <summary>
    /// Flying speed for players in creative/spectator when controlled.
    /// Source: LivingEntity.getFlyingSpeed()
    /// </summary>
    public const float DefaultFlyingSpeed = 0.02f;

    // ===== Collisions =====
    
    /// <summary>
    /// Minimum movement distance before being zeroed.
    /// Source: LivingEntity.MIN_MOVEMENT_DISTANCE
    /// </summary>
    public const double MinMovementDistance = 0.003;

    /// <summary>
    /// Maximum step height for automatic stepping up blocks.
    /// Source: Attributes.STEP_HEIGHT default for players
    /// </summary>
    public const double DefaultStepHeight = 1.0;

    /// <summary>
    /// Player bounding box width.
    /// </summary>
    public const double PlayerWidth = 0.6;

    /// <summary>
    /// Player bounding box height (standing).
    /// </summary>
    public const double PlayerHeight = 1.8;

    /// <summary>
    /// Player eye height (standing).
    /// </summary>
    public const double PlayerEyeHeight = 1.62;

    /// <summary>
    /// Player bounding box height (sneaking).
    /// </summary>
    public const double PlayerSneakingHeight = 1.5;

    // ===== Ladder/Climbing =====
    
    /// <summary>
    /// Maximum horizontal velocity when on a ladder.
    /// Source: LivingEntity.handleOnClimbable()
    /// </summary>
    public const double MaxClimbSpeed = 0.15;

    /// <summary>
    /// Upward velocity when climbing and pressing forward.
    /// Source: handleRelativeFrictionAndCalculateMovement()
    /// </summary>
    public const double ClimbUpSpeed = 0.2;

    // ===== Water/Fluid =====
    
    /// <summary>
    /// Water slowdown multiplier.
    /// Source: LivingEntity.getWaterSlowDown()
    /// </summary>
    public const float WaterSlowdown = 0.8f;

    /// <summary>
    /// Water movement acceleration.
    /// Source: travelInWater()
    /// </summary>
    public const float WaterAcceleration = 0.02f;

    /// <summary>
    /// Sinking impulse when pressing shift in water.
    /// Source: LivingEntity.goDownInWater()
    /// </summary>
    public const double WaterSinkImpulse = 0.04;

    /// <summary>
    /// Rising impulse when jumping in water.
    /// Source: LivingEntity.jumpInLiquid()
    /// </summary>
    public const double WaterJumpImpulse = 0.04;

    // ===== Entity Collision =====
    
    /// <summary>
    /// Entity collision push strength base.
    /// </summary>
    public const double EntityPushStrength = 0.05;

    /// <summary>
    /// Maximum velocity from entity collision.
    /// </summary>
    public const double MaxEntityPushVelocity = 0.15;

    // ===== Knockback =====
    
    /// <summary>
    /// Default knockback strength.
    /// Source: LivingEntity.DEFAULT_KNOCKBACK = 0.4F
    /// </summary>
    public const float DefaultKnockback = 0.4f;

    // ===== Epsilon =====
    
    /// <summary>
    /// Small value for floating point comparisons.
    /// </summary>
    public const double Epsilon = 1.0E-7;
}
