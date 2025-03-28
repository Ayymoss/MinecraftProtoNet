using System.Diagnostics.CodeAnalysis;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Packets.Base.Definitions;

namespace MinecraftProtoNet.State;

public class Entity
{
    public const double PlayerWidth = 0.6;
    public const double PlayerHeight = 1.8;
    public const double PlayerEyeHeight = 1.62;
    private const double HalfWidth = PlayerWidth / 2.0;

    public int EntityId { get; set; }
    public float Health { get; set; }
    public int Hunger { get; set; }
    public float HungerSaturation { get; set; }
    private Vector3<double> _position = new();
    public Vector3<double> Velocity { get; set; } = new();
    public Vector2<float> YawPitch { get; set; } = new();
    public bool IsOnGround { get; set; }

    public Vector3<double> Position
    {
        get => _position;
        set => _position = value;
    }

    private int _blockPlaceSequence;
    public int BlockPlaceSequence => _blockPlaceSequence; // Not thread safe

    public int IncrementSequence()
    {
        var currentSequence = _blockPlaceSequence;
        Interlocked.Increment(ref _blockPlaceSequence);
        return currentSequence;
    }

    public short HeldSlot { get; set; }
    public short HeldSlotWithOffset => (short)(HeldSlot + 36);
    public Slot HeldItem => Inventory.TryGetValue(HeldSlotWithOffset, out var slot) ? slot : Slot.Empty; // Safer access
    public Dictionary<short, Slot> Inventory { get; set; } = new();
    public bool IsJumping { get; private set; }
    public bool IsSneaking { get; private set; }
    public bool WantsToSprint { get; private set; }
    public bool IsSprintingNew { get; set; }

    [MemberNotNullWhen(true, nameof(IsHurtFromYaw))]
    public bool IsHurt => IsHurtFromYaw is not null;

    public float? IsHurtFromYaw { get; set; }
    public bool Forward { get; set; }
    public bool Backward { get; set; }
    public bool Left { get; set; }
    public bool Right { get; set; }


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

    public Vector3<double> GetLookDirection()
    {
        var yawRad = Math.PI / 180 * YawPitch.X;
        var pitchRad = Math.PI / 180 * YawPitch.Y;

        var x = -Math.Sin(yawRad) * Math.Cos(pitchRad);
        var y = -Math.Sin(pitchRad);
        var z = Math.Cos(yawRad) * Math.Cos(pitchRad);

        return new Vector3<double>(x, y, z);
    }

    public RaycastHit? GetLookingAtBlock(Level level, double maxDistance = 5.0)
    {
        var start = Position + new Vector3<double>(0, PlayerEyeHeight, 0);
        var direction = GetLookDirection();
        return level.RayCast(start, direction, maxDistance);
    }

    public Entity? GetLookingAtEntity(Entity localPlayer, IEnumerable<Entity> entities, double maxDistance = 10.0)
    {
        var start = localPlayer.Position + new Vector3<double>(0, PlayerEyeHeight, 0);
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
        var eyePos = localPlayer.Position + new Vector3<double>(0, PlayerEyeHeight, 0);
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

    public void StartJumping() => IsJumping = true;
    public void StopJumping() => IsJumping = false;
    public void StartSprinting() => WantsToSprint = true;
    public void StopSprinting() => WantsToSprint = false;
    public void StartSneaking() => IsSneaking = true;
    public void StopSneaking() => IsSneaking = false;
}
