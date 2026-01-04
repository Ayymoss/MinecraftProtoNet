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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/IWaypoint.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Cache;

/// <summary>
/// A marker for a position in the world.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/IWaypoint.java
/// </summary>
public interface IWaypoint
{
    /// <summary>
    /// Gets the label for this waypoint.
    /// </summary>
    string GetName();

    /// <summary>
    /// Returns the tag for this waypoint.
    /// </summary>
    Tag GetTag();

    /// <summary>
    /// Returns the unix epoch time in milliseconds that this waypoint was created.
    /// </summary>
    long GetCreationTimestamp();

    /// <summary>
    /// Returns the actual block position of this waypoint.
    /// </summary>
    BetterBlockPos GetLocation();

    /// <summary>
    /// Waypoint tag enum.
    /// </summary>
    public enum Tag
    {
        /// <summary>
        /// Tag indicating a position explicitly marked as a home base.
        /// </summary>
        Home,

        /// <summary>
        /// Tag indicating a position that the local player has died at.
        /// </summary>
        Death,

        /// <summary>
        /// Tag indicating a bed position.
        /// </summary>
        Bed,

        /// <summary>
        /// Tag indicating that the waypoint was user-created.
        /// </summary>
        User
    }
}

/// <summary>
/// Extension methods for IWaypoint.Tag.
/// </summary>
public static class WaypointTagExtensions
{
    /// <summary>
    /// Gets a tag by one of its names.
    /// </summary>
    public static IWaypoint.Tag? GetByName(string name)
    {
        var nameLower = name.ToLowerInvariant();
        return nameLower switch
        {
            "home" or "base" => IWaypoint.Tag.Home,
            "death" => IWaypoint.Tag.Death,
            "bed" or "spawn" => IWaypoint.Tag.Bed,
            "user" => IWaypoint.Tag.User,
            _ => null
        };
    }

    /// <summary>
    /// Gets all tag names.
    /// </summary>
    public static string[] GetAllNames()
    {
        return new[] { "home", "base", "death", "bed", "spawn", "user" };
    }
}

