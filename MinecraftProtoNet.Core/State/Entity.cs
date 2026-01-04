using System.Diagnostics.CodeAnalysis;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Physics;
using MinecraftProtoNet.Physics.Shapes;
using InputModel = MinecraftProtoNet.Models.Input.Input;

namespace MinecraftProtoNet.State;

/// <summary>
/// Represents an entity in the game world.
/// Stores both physics state and input state for players.
/// </summary>
public class Entity
{
    // ===== Bounding Box Constants (from PhysicsConstants for reference) =====
    public const double PlayerWidth = PhysicsConstants.PlayerWidth;
    public const double PlayerHeight = PhysicsConstants.PlayerHeight;
    public const double PlayerEyeHeight = PhysicsConstants.PlayerEyeHeight;
    private const double HalfWidth = PlayerWidth / 2.0;

    // ===== Identity =====
    public int EntityId { get; set; }

    /// <summary>
    /// Event fired when health, hunger, or saturation changes.
    /// </summary>
    public event Action? OnStatsChanged;

    // ===== Health & Hunger ======
    public float Health
    {
        get;
        set
        {
            if (!(Math.Abs(field - value) > 0.01f)) return;
            field = value;
            OnStatsChanged?.Invoke();
        }
    } = 20f;

    public int Hunger
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnStatsChanged?.Invoke();
        }
    } = 20;

    public float HungerSaturation
    {
        get;
        set
        {
            if (!(Math.Abs(field - value) > 0.01f)) return;
            field = value;
            OnStatsChanged?.Invoke();
        }
    } = 5f;

    // ===== Position & Movement =====
    private Vector3<double> _position = new();

    public Vector3<double> Position
    {
        get => _position;
        set => _position = value;
    }

    public Vector3<double> Velocity { get; set; } = new();
    public Vector2<float> YawPitch { get; set; } = new();

    // ===== Physics State =====
    public bool IsOnGround { get; set; }
    public bool HorizontalCollision { get; set; }

    /// <summary>
    /// Whether the entity is currently submerged in water.
    /// </summary>
    public bool IsInWater { get; set; }

    /// <summary>
    /// Whether the entity is currently submerged in lava.
    /// </summary>
    public bool IsInLava { get; set; }

    /// <summary>
    /// The height of the fluid the entity is currently in (0.0 to 1.0ish).
    /// </summary>
    public double FluidHeight { get; set; }

    /// <summary>
    /// Whether the entity has a pending teleport to acknowledge in the next physics tick.
    /// </summary>
    public bool HasPendingTeleport { get; set; }

    /// <summary>
    /// Event fired when the server sends a teleport packet.
    /// Used by pathfinding to detect teleport loops vs collision-based stuck states.
    /// </summary>
    public event Action<Vector3<double>>? OnServerTeleport;

    /// <summary>
    /// Notifies listeners that the server has sent a teleport packet.
    /// </summary>
    public void NotifyServerTeleport(Vector3<double> position)
    {
        OnServerTeleport?.Invoke(position);
    }

    /// <summary>
    /// The exact Yaw/Pitch sent by the server in the last teleport packet.
    /// Used to acknowledge teleports with 100% precision.
    /// </summary>
    public Vector2<float>? TeleportYawPitch { get; set; }

    /// <summary>
    /// Whether the entity is currently sprinting (server-confirmed state).
    /// This differs from Input.Sprint which is the intent to sprint.
    /// </summary>
    public bool IsSprinting { get; set; }

    /// <summary>
    /// Ticks remaining before the entity can jump again.
    /// Matches Mojang's noJumpDelay (10 ticks between jumps).
    /// </summary>
    public int JumpCooldown { get; set; }

    /// <summary>
    /// Whether the entity was sprinting in the previous tick.
    /// Used to detect sprint state changes for packet sending.
    /// </summary>
    public bool WasSprinting { get; set; }

    /// <summary>
    /// Whether the entity was sneaking in the previous tick.
    /// Used to detect sneak state changes for packet sending.
    /// </summary>
    public bool WasSneaking { get; set; }

    // ===== Input State =====
    public InputState InputState { get; } = new();

    /// <summary>
    /// Current input state for this tick.
    /// Delegated to InputState.Current for backward compat.
    /// </summary>
    public InputModel Input
    {
        get => InputState.Current;
        set => InputState.Current = value;
    }

    /// <summary>
    /// Previous tick's input state.
    /// Delegated to InputState.LastSent for backward compat.
    /// </summary>
    public InputModel LastSentInput
    {
        get => InputState.LastSent;
        set => InputState.LastSent = value;
    }

    // ===== Legacy Input Properties (Delegates) =====

    public bool Forward
    {
        get => InputState.Current.Forward;
        set => InputState.SetForward(value);
    }

    public bool Backward
    {
        get => InputState.Current.Backward;
        set => InputState.SetBackward(value);
    }

    public bool Left
    {
        get => InputState.Current.Left;
        set => InputState.SetLeft(value);
    }

    public bool Right
    {
        get => InputState.Current.Right;
        set => InputState.SetRight(value);
    }

    public bool IsJumping => InputState.Current.Jump;
    public bool IsSneaking => InputState.Current.Shift;
    public bool WantsToSprint => InputState.Current.Sprint;

    // ===== Input Methods (Delegates) =====

    public void StartJumping() => InputState.SetJump(true);
    public void StopJumping() => InputState.SetJump(false);
    public void StartSprinting() => InputState.SetSprint(true);
    public void StopSprinting() => InputState.SetSprint(false);
    public void StartSneaking() => InputState.SetSneak(true);
    public void StopSneaking() => InputState.SetSneak(false);

    public void ClearMovementInput() => InputState.ClearMovement();

    // ===== Damage State =====
    [MemberNotNullWhen(true, nameof(HurtFromYaw))]
    public bool IsHurt => HurtFromYaw is not null;

    /// <summary>
    /// Yaw direction from which damage was received. Null if not hurt.
    /// </summary>
    public float? HurtFromYaw { get; set; }

    /// <summary>
    /// The entity's inventory.
    /// </summary>
    public EntityInventory Inventory { get; } = new();

    /// <summary>
    /// Currently open container/menu (null if no container is open).
    /// </summary>
    public ContainerState? CurrentContainer { get; set; }

    /// <summary>
    /// Event fired when a container is opened.
    /// </summary>
    public event Action<ContainerState>? OnContainerOpened;

    /// <summary>
    /// Notifies listeners that a container has been opened.
    /// </summary>
    public void NotifyContainerOpened(ContainerState container)
    {
        OnContainerOpened?.Invoke(container);
    }


    // Convenience accessors that delegate to Inventory
    public int BlockPlaceSequence => Inventory.BlockPlaceSequence;
    public int IncrementSequence() => Inventory.IncrementSequence();

    public short HeldSlot
    {
        get => Inventory.HeldSlot;
        set => Inventory.HeldSlot = value;
    }

    public short HeldSlotWithOffset => Inventory.HeldSlotWithOffset;
    public Slot HeldItem => Inventory.HeldItem;

    // ===== Bounding Box =====

    public AABB GetBoundingBox()
    {
        return new AABB(
            _position.X - HalfWidth,
            _position.Y,
            _position.Z - HalfWidth,
            _position.X + HalfWidth,
            _position.Y + PlayerHeight,
            _position.Z + HalfWidth
        );
    }

    public void UpdatePositionFromAABB(AABB box)
    {
        _position = new Vector3<double>(
            box.Min.X + HalfWidth,
            box.Min.Y,
            box.Min.Z + HalfWidth
        );
    }

    // ===== Look Direction =====

    public Vector3<double> GetLookDirection()
    {
        var yawRad = Math.PI / 180 * YawPitch.X;
        var pitchRad = Math.PI / 180 * YawPitch.Y;

        var x = -Math.Sin(yawRad) * Math.Cos(pitchRad);
        var y = -Math.Sin(pitchRad);
        var z = Math.Cos(yawRad) * Math.Cos(pitchRad);

        return new Vector3<double>(x, y, z);
    }

    public Vector3<double> EyePosition => Position + new Vector3<double>(0, PlayerEyeHeight, 0);

    // ===== Raycasting =====

    public RaycastHit? GetLookingAtBlock(Level level, double maxDistance = 5.0)
    {
        return level.RayCast(EyePosition, GetLookDirection(), maxDistance);
    }

    public Entity? GetLookingAtEntity(Entity localPlayer, IEnumerable<Entity> entities, double maxDistance = 10.0)
    {
        var start = localPlayer.EyePosition;
        var direction = localPlayer.GetLookDirection();

        Entity? closestEntity = null;
        var closestHitDist = maxDistance * maxDistance;

        foreach (var entity in entities)
        {
            if (entity.EntityId == localPlayer.EntityId) continue;

            var targetBox = entity.GetBoundingBox().Expand(0.1);
            var toEntityCenter = (targetBox.Min + targetBox.Max) * 0.5 - start;
            var distSq = toEntityCenter.LengthSquared();

            if (distSq > maxDistance * maxDistance) continue;

            var dot = direction.Dot(toEntityCenter.Normalized());
            if (dot < 0.707) continue;

            var projectionLength = direction.Dot(toEntityCenter);
            if (projectionLength <= 0) continue;

            var pointOnRay = start + direction * projectionLength;
            if (!(pointOnRay.X >= targetBox.Min.X) || !(pointOnRay.X <= targetBox.Max.X) ||
                !(pointOnRay.Y >= targetBox.Min.Y) || !(pointOnRay.Y <= targetBox.Max.Y) ||
                !(pointOnRay.Z >= targetBox.Min.Z) || !(pointOnRay.Z <= targetBox.Max.Z)) continue;

            if (!(distSq < closestHitDist)) continue;

            closestHitDist = distSq;
            closestEntity = entity;
        }

        return closestEntity;
    }

    public Vector2<float> GetYawPitchToTarget(Entity localPlayer, Entity targetEntity)
    {
        var targetCenter = (targetEntity.GetBoundingBox().Min + targetEntity.GetBoundingBox().Max) * 0.5;
        var eyePos = localPlayer.EyePosition;
        var toTarget = targetCenter - eyePos;
        var yaw = (float)(Math.Atan2(-toTarget.X, toTarget.Z) * (180.0 / Math.PI));
        var horizontalDistance = Math.Sqrt(toTarget.X * toTarget.X + toTarget.Z * toTarget.Z);
        var pitch = (float)(Math.Atan2(-toTarget.Y, horizontalDistance) * (180.0 / Math.PI));

        yaw %= 360;
        if (yaw > 180) yaw -= 360;
        if (yaw <= -180) yaw += 360;
        pitch = Math.Clamp(pitch, -90, 90);

        return new Vector2<float>(yaw, pitch);
    }

    // ===== Block Position =====

    /// <summary>
    /// Returns the block position of this entity (floor of position coordinates).
    /// Equivalent to Java's Entity.blockPosition() which returns BlockPos containing floored position.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:3721-3723
    /// </summary>
    public Vector3<int> BlockPosition()
    {
        return new Vector3<int>(
            (int)Math.Floor(Position.X),
            (int)Math.Floor(Position.Y),
            (int)Math.Floor(Position.Z)
        );
    }
}
