using MinecraftProtoNet.Models.Input;

namespace MinecraftProtoNet.State;

/// <summary>
/// Manages the input state for an entity (player).
/// Decouples raw input data from the Entity class.
/// </summary>
public class InputState
{
    /// <summary>
    /// Current input state for this tick.
    /// </summary>
    public Input Current { get; set; } = Input.Empty;

    /// <summary>
    /// Previous tick's input state (or last sent state).
    /// Used to detect changes for packet sending.
    /// </summary>
    public Input LastSent { get; set; } = Input.Empty;

    // ===== Helper Methods for Modifying State =====

    public void SetForward(bool value) => Current = Current with { Forward = value };
    public void SetBackward(bool value) => Current = Current with { Backward = value };
    public void SetLeft(bool value) => Current = Current with { Left = value };
    public void SetRight(bool value) => Current = Current with { Right = value };
    public void SetJump(bool value) => Current = Current with { Jump = value };
    public void SetSneak(bool value) => Current = Current with { Shift = value };
    public void SetSprint(bool value) => Current = Current with { Sprint = value };

    /// <summary>
    /// Clears all movement input.
    /// </summary>
    public void ClearMovement()
    {
        Current = Input.Empty;
    }
}
