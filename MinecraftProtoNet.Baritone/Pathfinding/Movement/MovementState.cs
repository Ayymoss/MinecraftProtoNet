using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// Mutable state for executing a movement.
/// Based on Baritone's MovementState.
/// </summary>
public class MovementState
{
    /// <summary>
    /// Current status of the movement.
    /// </summary>
    public MovementStatus Status { get; set; } = MovementStatus.Waiting;

    /// <summary>
    /// Target rotation (yaw, pitch) if any.
    /// </summary>
    public (float Yaw, float Pitch)? TargetRotation { get; set; }

    /// <summary>
    /// Whether to force the rotation.
    /// </summary>
    public bool ForceRotation { get; set; }

    // ===== Input States =====

    public bool MoveForward { get; set; }
    public bool MoveBackward { get; set; }
    public bool MoveLeft { get; set; }
    public bool MoveRight { get; set; }
    public bool Jump { get; set; }
    public bool Sneak { get; set; }
    public bool Sprint { get; set; }
    public bool LeftClick { get; set; }
    public bool RightClick { get; set; }
    
    /// <summary>
    /// Target block position to place a block at (if RightClick is true).
    /// </summary>
    public (int X, int Y, int Z)? PlaceBlockTarget { get; set; }

    /// <summary>
    /// Target block position to break (if LeftClick is true or implicit).
    /// </summary>
    public (int X, int Y, int Z)? BreakBlockTarget { get; set; }

    /// <summary>
    /// Clears all input states.
    /// </summary>
    public void ClearInputs()
    {
        MoveForward = false;
        MoveBackward = false;
        MoveLeft = false;
        MoveRight = false;
        Jump = false;
        Sneak = false;
        Sprint = false;
        LeftClick = false;
        RightClick = false;
    }

    /// <summary>
    /// Sets the target rotation.
    /// </summary>
    public MovementState SetTarget(float yaw, float pitch, bool force = true)
    {
        TargetRotation = (yaw, pitch);
        ForceRotation = force;
        return this;
    }

    /// <summary>
    /// Sets the movement status.
    /// </summary>
    public MovementState SetStatus(MovementStatus status)
    {
        Status = status;
        return this;
    }
}
