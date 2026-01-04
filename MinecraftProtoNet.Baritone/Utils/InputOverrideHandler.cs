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
                
                // Update movement inputs
                inputState.SetForward(IsInputForcedDown(Input.MoveForward));
                inputState.SetBackward(IsInputForcedDown(Input.MoveBack));
                inputState.SetLeft(IsInputForcedDown(Input.MoveLeft));
                inputState.SetRight(IsInputForcedDown(Input.MoveRight));
                inputState.SetJump(IsInputForcedDown(Input.Jump));
                inputState.SetSneak(IsInputForcedDown(Input.Sneak));
                inputState.SetSprint(IsInputForcedDown(Input.Sprint));
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

