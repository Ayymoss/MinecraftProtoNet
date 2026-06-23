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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementOption.java
 */

using MinecraftProtoNet.Baritone.Api.Utils.Input;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// A candidate combination of movement inputs and the world-space motion vector it produces.
/// Used to move toward an ideal yaw without rotating the player's head (strafing/back-walking).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/MovementOption.java
/// </summary>
public sealed record MovementOption(Input? Input1, Input? Input2, float MotionX, float MotionZ)
{
    private const float SprintMultiplier = 1.3f;

    public MovementOption(Input input1, float motionX, float motionZ) : this(input1, null, motionX, motionZ)
    {
    }

    public void SetInputs(MovementState movementState)
    {
        if (Input1 != null)
        {
            movementState.SetInput(Input1.Value, true);
        }
        if (Input2 != null)
        {
            movementState.SetInput(Input2.Value, true);
        }
    }

    public float DistanceToSq(float otherX, float otherZ)
    {
        return Math.Abs(MotionX - otherX) + Math.Abs(MotionZ - otherZ);
    }

    public static IEnumerable<MovementOption> GetOptions(float motionX, float motionZ, bool canSprint)
    {
        return
        [
            new MovementOption(Input.MoveForward, canSprint ? motionX * SprintMultiplier : motionX, canSprint ? motionZ * SprintMultiplier : motionZ),
            new MovementOption(Input.MoveBack, -motionX, -motionZ),
            new MovementOption(Input.MoveLeft, -motionZ, motionX),
            new MovementOption(Input.MoveRight, motionZ, -motionX),
            new MovementOption(Input.MoveForward, Input.MoveLeft, (canSprint ? motionX * SprintMultiplier : motionX) - motionZ, (canSprint ? motionZ * SprintMultiplier : motionZ) + motionX),
            new MovementOption(Input.MoveForward, Input.MoveRight, (canSprint ? motionX * SprintMultiplier : motionX) + motionZ, (canSprint ? motionZ * SprintMultiplier : motionZ) - motionX),
            new MovementOption(Input.MoveBack, Input.MoveLeft, -motionX - motionZ, -motionZ + motionX),
            new MovementOption(Input.MoveBack, Input.MoveRight, -motionX + motionZ, -motionZ - motionX),
        ];
    }
}
