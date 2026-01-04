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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementState.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// State tracking for movement execution.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementState.java
/// </summary>
public class MovementState
{
    private MovementStatus _status;
    private MovementTarget _target = new();
    private readonly Dictionary<Input, bool> _inputState = new();

    public MovementState SetStatus(MovementStatus status)
    {
        _status = status;
        return this;
    }

    public MovementStatus GetStatus() => _status;

    public MovementTarget GetTarget() => _target;

    public MovementState SetTarget(MovementTarget target)
    {
        _target = target;
        return this;
    }

    public MovementState SetInput(Input input, bool forced)
    {
        _inputState[input] = forced;
        return this;
    }

    public Dictionary<Input, bool> GetInputStates() => _inputState;

    public class MovementTarget
    {
        /// <summary>
        /// Yaw and pitch angles that must be matched
        /// </summary>
        public Rotation? Rotation;

        /// <summary>
        /// Whether or not this target must force rotations.
        /// true if we're trying to place or break blocks, false if we're trying to look at the movement location
        /// </summary>
        private bool _forceRotations;

        public MovementTarget()
        {
            Rotation = null;
            _forceRotations = false;
        }

        public MovementTarget(Rotation? rotation, bool forceRotations)
        {
            Rotation = rotation;
            _forceRotations = forceRotations;
        }

        public Rotation? GetRotation() => Rotation;

        public bool HasToForceRotations() => _forceRotations;
    }
}

