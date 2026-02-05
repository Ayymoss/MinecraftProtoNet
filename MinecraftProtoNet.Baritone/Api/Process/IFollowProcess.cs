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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IFollowProcess.java
 */

namespace MinecraftProtoNet.Baritone.Api.Process;

/// <summary>
/// Interface for follow process.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IFollowProcess.java
/// </summary>
public interface IFollowProcess : IBaritoneProcess
{
    /// <summary>
    /// Set the follow target to any entities matching this predicate.
    /// </summary>
    void Follow(Predicate<object> filter);

    /// <summary>
    /// Try to pick up any items matching this predicate.
    /// </summary>
    void Pickup(Predicate<object> filter);

    /// <summary>
    /// Gets the entities that are currently being followed. null if not currently following, empty if nothing matches the predicate.
    /// </summary>
    IReadOnlyList<object> Following();

    /// <summary>
    /// Gets the current filter.
    /// </summary>
    Predicate<object>? CurrentFilter();

    /// <summary>
    /// Cancels the follow behavior, this will clear the current follow target.
    /// </summary>
    void Cancel();
}

