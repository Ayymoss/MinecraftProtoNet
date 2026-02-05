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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/movement/MovementStatus.java
 */

namespace MinecraftProtoNet.Baritone.Api.Pathing.Movement;

/// <summary>
/// Status of a movement.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/movement/MovementStatus.java
/// </summary>
public enum MovementStatus
{
    /// <summary>
    /// We are preparing the movement to be executed. This is when any blocks obstructing the destination are broken.
    /// </summary>
    Prepping,

    /// <summary>
    /// We are waiting for the movement to begin, after PREPPING.
    /// </summary>
    Waiting,

    /// <summary>
    /// The movement is currently in progress, after WAITING.
    /// </summary>
    Running,

    /// <summary>
    /// The movement has been completed and we are at our destination.
    /// </summary>
    Success,

    /// <summary>
    /// There was a change in state between calculation and actual movement execution, and the movement has now become impossible.
    /// </summary>
    Unreachable,

    /// <summary>
    /// Unused.
    /// </summary>
    Failed,

    /// <summary>
    /// "Unused".
    /// </summary>
    Canceled
}

/// <summary>
/// Extension methods for MovementStatus.
/// </summary>
public static class MovementStatusExtensions
{
    /// <summary>
    /// Returns whether this status indicates a complete movement.
    /// </summary>
    public static bool IsComplete(this MovementStatus status)
    {
        return status switch
        {
            MovementStatus.Success => true,
            MovementStatus.Unreachable => true,
            MovementStatus.Failed => true,
            MovementStatus.Canceled => true,
            _ => false
        };
    }
}

