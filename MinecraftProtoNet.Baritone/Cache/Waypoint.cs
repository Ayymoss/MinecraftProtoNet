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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/Waypoint.java
 */

using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Cache;

/// <summary>
/// Waypoint implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/Waypoint.java
/// </summary>
public class Waypoint : IWaypoint
{
    private readonly string _name;
    private readonly IWaypoint.Tag _tag;
    private readonly BetterBlockPos _location;
    private readonly long _creationTimestamp;

    public Waypoint(string name, IWaypoint.Tag tag, BetterBlockPos location)
        : this(name, tag, location, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
    {
    }

    public Waypoint(string name, IWaypoint.Tag tag, BetterBlockPos location, long creationTimestamp)
    {
        _name = name;
        _tag = tag;
        _location = location;
        _creationTimestamp = creationTimestamp;
    }

    public string GetName() => _name;
    public IWaypoint.Tag GetTag() => _tag;
    public BetterBlockPos GetLocation() => _location;
    public long GetCreationTimestamp() => _creationTimestamp;
}

