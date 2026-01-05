/*
 * This file is part of Baritone.
 *
 * Baritone is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Baritone is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with Baritone.  If not, see <https://www.gnu.org/licenses/>.
 *
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/InputOverrideHandler.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Behavior;
using MinecraftProtoNet.Baritone.Api.Event.Events;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Behaviors;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Input override handler implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/InputOverrideHandler.java
/// </summary>
public class InputOverrideHandler : Behavior, IInputOverrideHandler
{
    private readonly Dictionary<Input, bool> _forcedInputs = new();
    private int _tickCounter = 0;

    public InputOverrideHandler(IBaritone baritone) : base(baritone)
    {
    }

    public bool IsInputForcedDown(Input input)
    {
        return _forcedInputs.GetValueOrDefault(input, false);
    }

    public void SetInputForceState(Input input, bool forced)
    {
        _forcedInputs[input] = forced;
    }

    public void ClearAllKeys()
    {
        _forcedInputs.Clear();
        
        // CRITICAL: Immediately clear input state on entity to stop movement
        // This ensures inputs are cleared even if OnTick() has already run this tick
        var player = Ctx.Player() as Entity;
        if (player != null)
        {
            var inputState = player.InputState;
            inputState.SetForward(false);
            inputState.SetBackward(false);
            inputState.SetLeft(false);
            inputState.SetRight(false);
            inputState.SetJump(false);
            inputState.SetSneak(false);
            inputState.SetSprint(false);
        }
    }

    /// <summary>
    /// Updates Entity.InputState.Current based on forced inputs.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/InputOverrideHandler.java:86-107
    /// </summary>
    public override void OnTick(TickEvent evt)
    {
        if (evt.GetType() == TickEvent.TickEventType.Out)
        {
            return;
        }

        // If left click is forced, disable right click (they can't both be active)
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/InputOverrideHandler.java:90-92
        if (IsInputForcedDown(Input.ClickLeft))
        {
            SetInputForceState(Input.ClickRight, false);
        }

        // Update Entity.InputState.Current based on forced inputs
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/InputOverrideHandler.java:96-104
        if (InControl())
        {
            var player = Ctx.Player() as Entity;
            if (player != null)
            {
                var inputState = player.InputState;
                
                // Get input states before updating
                var forward = IsInputForcedDown(Input.MoveForward);
                var backward = IsInputForcedDown(Input.MoveBack);
                var left = IsInputForcedDown(Input.MoveLeft);
                var right = IsInputForcedDown(Input.MoveRight);
                var jump = IsInputForcedDown(Input.Jump);
                var sneak = IsInputForcedDown(Input.Sneak);
                var sprint = IsInputForcedDown(Input.Sprint);
                
                // Update movement inputs
                inputState.SetForward(forward);
                inputState.SetBackward(backward);
                inputState.SetLeft(left);
                inputState.SetRight(right);
                inputState.SetJump(jump);
                inputState.SetSneak(sneak);
                inputState.SetSprint(sprint);
                
                // Reduced logging - only log every 100 ticks
                _tickCounter++;
                if (_tickCounter % 100 == 0)
                {
                    var pos = Ctx.PlayerFeet();
                    var rot = Ctx.PlayerRotations();
                    Baritone.GetGameEventHandler().LogDirect(
                        $"Input: pos={pos}, rot={rot?.GetYaw():F1}°, inputs=[F:{forward} B:{backward} L:{left} R:{right}]");
                }
            }
        }
    }

    /// <summary>
    /// Checks if Baritone is in control (has any forced movement inputs or is pathing).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/InputOverrideHandler.java:109-117
    /// </summary>
    private bool InControl()
    {
        // Check if any movement input is forced
        foreach (var input in new[] { Input.MoveForward, Input.MoveBack, Input.MoveLeft, Input.MoveRight, Input.Sneak, Input.Jump })
        {
            if (IsInputForcedDown(input))
            {
                return true;
            }
        }
        
        // If pathing, also consider us in control
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/InputOverrideHandler.java:116
        var pathingBehavior = Baritone.GetPathingBehavior();
        return pathingBehavior.IsPathing();
    }
}

