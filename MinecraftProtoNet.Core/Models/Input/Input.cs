namespace MinecraftProtoNet.Core.Models.Input;

/// <summary>
/// Represents player input state for a single tick.
/// Matches Java's net.minecraft.world.entity.player.Input record.
/// </summary>
/// <param name="Forward">W key / forward movement</param>
/// <param name="Backward">S key / backward movement</param>
/// <param name="Left">A key / strafe left</param>
/// <param name="Right">D key / strafe right</param>
/// <param name="Jump">Space key / jump</param>
/// <param name="Shift">Shift key / sneak</param>
/// <param name="Sprint">Sprint key (Ctrl) / sprint intent</param>
public readonly record struct Input(
    bool Forward,
    bool Backward,
    bool Left,
    bool Right,
    bool Jump,
    bool Shift,
    bool Sprint)
{
    /// <summary>
    /// Empty input with no keys pressed.
    /// </summary>
    public static readonly Input Empty = new(false, false, false, false, false, false, false);

    /// <summary>
    /// Returns true if any movement key is pressed.
    /// </summary>
    public bool HasMovement => Forward || Backward || Left || Right;

    /// <summary>
    /// Returns true if forward impulse is active (Forward pressed, not cancelled by Backward).
    /// </summary>
    public bool HasForwardImpulse => Forward && !Backward;

    /// <summary>
    /// Gets the move vector as (strafeX, forwardZ) where:
    /// - Positive X = right, Negative X = left
    /// - Positive Z = forward, Negative Z = backward
    /// Values are in range [-1, 1].
    /// </summary>
    public (float X, float Z) GetMoveVector()
    {
        float x = 0f;
        float z = 0f;

        if (Forward) z += 1f;
        if (Backward) z -= 1f;
        if (Left) x -= 1f;
        if (Right) x += 1f;

        return (x, z);
    }

    /// <summary>
    /// Gets a normalized move vector where diagonal movement doesn't exceed speed 1.
    /// </summary>
    public (float X, float Z) GetNormalizedMoveVector()
    {
        var (x, z) = GetMoveVector();
        var lengthSq = x * x + z * z;

        if (lengthSq > 1f)
        {
            var length = MathF.Sqrt(lengthSq);
            x /= length;
            z /= length;
        }

        return (x, z);
    }

    /// <summary>
    /// Creates a new Input with the jump flag set.
    /// Used for auto-jump functionality.
    /// </summary>
    public Input WithJump() => this with { Jump = true };

    /// <summary>
    /// Creates a new Input with sprint enabled.
    /// </summary>
    public Input WithSprint() => this with { Sprint = true };

    /// <summary>
    /// Serializes to a single byte for network transmission.
    /// Matches Java's Input.STREAM_CODEC format.
    /// </summary>
    public byte ToByte()
    {
        byte flags = 0;
        if (Forward) flags |= 1;
        if (Backward) flags |= 2;
        if (Left) flags |= 4;
        if (Right) flags |= 8;
        if (Jump) flags |= 16;
        if (Shift) flags |= 32;
        if (Sprint) flags |= 64;
        return flags;
    }

    /// <summary>
    /// Deserializes from a single byte.
    /// </summary>
    public static Input FromByte(byte flags) => new(
        Forward: (flags & 1) != 0,
        Backward: (flags & 2) != 0,
        Left: (flags & 4) != 0,
        Right: (flags & 8) != 0,
        Jump: (flags & 16) != 0,
        Shift: (flags & 32) != 0,
        Sprint: (flags & 64) != 0
    );
}
