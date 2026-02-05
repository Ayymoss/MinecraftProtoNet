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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/PathingCommandType.java
 */

namespace MinecraftProtoNet.Baritone.Api.Process;

/// <summary>
/// Type of pathing command.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/PathingCommandType.java
/// </summary>
public enum PathingCommandType
{
    /// <summary>
    /// Set the goal and path.
    /// If you use this alongside a null goal, it will continue along its current path and current goal.
    /// </summary>
    SetGoalAndPath,

    /// <summary>
    /// Has no effect on the current goal or path, just requests a pause.
    /// </summary>
    RequestPause,

    /// <summary>
    /// Set the goal (regardless of null), and request a cancel of the current path (when safe).
    /// </summary>
    CancelAndSetGoal,

    /// <summary>
    /// Set the goal and path.
    /// If cancelOnGoalInvalidation is true, revalidate the current goal, and cancel if it's no longer valid, or if the new goal is null.
    /// </summary>
    RevalidateGoalAndPath,

    /// <summary>
    /// Set the goal and path.
    /// Cancel the current path if the goals are not equal.
    /// </summary>
    ForceRevalidateGoalAndPath,

    /// <summary>
    /// Go and ask the next process what to do.
    /// </summary>
    Defer,

    /// <summary>
    /// Sets the goal and calculates a path, but pauses instead of immediately starting the path.
    /// </summary>
    SetGoalAndPause
}

