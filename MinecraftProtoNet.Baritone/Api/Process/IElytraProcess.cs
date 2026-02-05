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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IElytraProcess.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Process;

/// <summary>
/// Interface for elytra process.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IElytraProcess.java
/// </summary>
public interface IElytraProcess : IBaritoneProcess
{
    /// <summary>
    /// Repacks chunks for elytra pathfinding.
    /// </summary>
    void RepackChunks();

    /// <summary>
    /// Gets where it is currently flying to, null if not active.
    /// </summary>
    BetterBlockPos? CurrentDestination();

    /// <summary>
    /// Paths to the specified destination.
    /// </summary>
    void PathTo(BetterBlockPos destination);

    /// <summary>
    /// Paths to the specified goal.
    /// </summary>
    void PathTo(Goal destination);

    /// <summary>
    /// Resets the state of the process but will maintain the same destination and will try to keep flying.
    /// </summary>
    void ResetState();

    /// <summary>
    /// Returns true if the native library loaded and elytra is actually usable.
    /// </summary>
    bool IsLoaded();

    /// <summary>
    /// FOR INTERNAL USE ONLY. Returns whether it's safe to cancel.
    /// </summary>
    bool IsSafeToCancel();
}

