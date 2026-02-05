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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/IWaypointCollection.java
 */

namespace MinecraftProtoNet.Baritone.Api.Cache;

/// <summary>
/// Interface for waypoint collection.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/IWaypointCollection.java
/// </summary>
public interface IWaypointCollection
{
    /// <summary>
    /// Adds a waypoint to this collection.
    /// </summary>
    void AddWaypoint(IWaypoint waypoint);

    /// <summary>
    /// Removes a waypoint from this collection.
    /// </summary>
    void RemoveWaypoint(IWaypoint waypoint);

    /// <summary>
    /// Gets the most recently created waypoint by the specified tag.
    /// </summary>
    IWaypoint? GetMostRecentByTag(IWaypoint.Tag tag);

    /// <summary>
    /// Gets all of the waypoints that have the specified tag.
    /// </summary>
    IReadOnlySet<IWaypoint> GetByTag(IWaypoint.Tag tag);

    /// <summary>
    /// Gets all of the waypoints in this collection, regardless of the tag.
    /// </summary>
    IReadOnlySet<IWaypoint> GetAllWaypoints();
}

