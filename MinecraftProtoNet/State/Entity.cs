using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Packets.Base.Definitions;

namespace MinecraftProtoNet.State;

public class Entity
{
    public int EntityId { get; set; }
    public Vector3<double> Position { get; set; } = new();
    public Vector3<double> Velocity { get; set; } = new();
    public Vector2<float> YawPitch { get; set; } = new();
    public bool IsOnGround { get; set; }

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
    public Slot HeldItem => Inventory[HeldSlotWithOffset];
    public Dictionary<short, Slot> Inventory { get; set; } = new();
    public bool IsJumping { get; set; }

    [MemberNotNullWhen(true, nameof(IsHurtFromYaw))]
    public bool IsHurt { get; set; }

    public float? IsHurtFromYaw { get; set; }

    public Vector3<double> GetLookDirection()
    {
        var yawRad = Math.PI / 180 * YawPitch.X;
        var pitchRad = Math.PI / 180 * YawPitch.Y;

        var x = -Math.Sin(yawRad) * Math.Cos(pitchRad);
        var y = -Math.Sin(pitchRad);
        var z = Math.Cos(yawRad) * Math.Cos(pitchRad);

        return new Vector3<double>(x, y, z);
    }

    public RaycastHit? GetLookingAtBlock(Level level, double maxDistance = 100.0)
    {
        var start = Position + new Vector3<double>(0, 1.62, 0);
        var direction = GetLookDirection();
        return level.RayCast(start, direction, maxDistance);
    }

    public bool CheckIsOnGround(Level level)
    {
        const double groundCheckDistance = 0.1;

        var feetPosition = new Vector3<double>(Position.X, Position.Y, Position.Z);
        var direction = new Vector3<double>(0, -1, 0);
        var hit = level.RayCast(feetPosition, direction, groundCheckDistance);

        var onGround = hit is { Distance: <= groundCheckDistance, Block: not null } &&
                       (!hit.Block.IsAir || !hit.Block.IsLiquid);

        return onGround;
    }
}
