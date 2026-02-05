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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/behavior/IPathingBehavior.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Pathing.Path;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Behavior;

/// <summary>
/// Interface for pathing behavior.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/behavior/IPathingBehavior.java
/// </summary>
public interface IPathingBehavior : IBehavior
{
    /// <summary>
    /// Returns the estimated remaining ticks in the current pathing segment.
    /// </summary>
    /// <param name="includeCurrentMovement">Whether to include the current movement</param>
    /// <returns>The estimated remaining ticks, or null if no segment</returns>
    double? TicksRemainingInSegment(bool includeCurrentMovement = true);

    /// <summary>
    /// Returns the estimated remaining ticks to the current goal.
    /// </summary>
    /// <returns>The estimated remaining ticks, or null if no goal</returns>
    double? EstimatedTicksToGoal();

    /// <summary>
    /// Gets the current pathing goal.
    /// </summary>
    Goal? GetGoal();

    /// <summary>
    /// Returns whether a path is currently being executed.
    /// </summary>
    bool IsPathing();

    /// <summary>
    /// Returns whether there is a current path.
    /// </summary>
    bool HasPath();

    /// <summary>
    /// Cancels the pathing behavior and all processes.
    /// </summary>
    /// <returns>Whether the pathing behavior was canceled</returns>
    bool CancelEverything();

    /// <summary>
    /// Force cancels everything (emergency stop).
    /// </summary>
    void ForceCancel();

    /// <summary>
    /// Gets the current path, if there is one.
    /// </summary>
    IPath? GetPath();

    /// <summary>
    /// Gets the current path finder being executed, if any.
    /// </summary>
    IPathFinder? GetInProgress();

    /// <summary>
    /// Gets the current path executor.
    /// </summary>
    IPathExecutor? GetCurrent();

    /// <summary>
    /// Gets the next path executor (planned ahead).
    /// </summary>
    IPathExecutor? GetNext();

    /// <summary>
    /// Gets the path start position.
    /// </summary>
    BetterBlockPos PathStart();

    /// <summary>
    /// Secret internal method to cancel the current segment.
    /// </summary>
    void SecretInternalSegmentCancel();

    /// <summary>
    /// Force revalidates the goal and path.
    /// </summary>
    void ForceRevalidateGoalAndPath();

    /// <summary>
    /// Revalidates the goal and path.
    /// </summary>
    void RevalidateGoalAndPath();
}

