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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/WaypointCollection.java
 */

using MinecraftProtoNet.Baritone.Api.Cache;

namespace MinecraftProtoNet.Baritone.Cache;

/// <summary>
/// Waypoint collection implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/WaypointCollection.java
/// </summary>
public class WaypointCollection : IWaypointCollection
{
    private const long WaypointMagicValue = 121977993584L;

    private readonly string _directory;
    private readonly Dictionary<IWaypoint.Tag, HashSet<IWaypoint>> _waypoints = new();

    public WaypointCollection(string directory)
    {
        _directory = directory;
        if (!Directory.Exists(_directory))
        {
            try
            {
                Directory.CreateDirectory(_directory);
            }
            catch (Exception)
            {
                // Ignore
            }
        }
        Load();
    }

    private void Load()
    {
        foreach (IWaypoint.Tag tag in Enum.GetValues<IWaypoint.Tag>())
        {
            Load(tag);
        }
    }

    private void Load(IWaypoint.Tag tag)
    {
        lock (_waypoints)
        {
            _waypoints[tag] = new HashSet<IWaypoint>();
        }

        var fileName = Path.Combine(_directory, tag.ToString().ToLowerInvariant() + ".mp4");
        if (!File.Exists(fileName))
        {
            return;
        }

        // Load implementation will be added when file I/O is integrated
    }

    private void Save(IWaypoint.Tag tag)
    {
        var fileName = Path.Combine(_directory, tag.ToString().ToLowerInvariant() + ".mp4");
        // Save implementation will be added when file I/O is integrated
    }

    public IReadOnlySet<IWaypoint> GetAllWaypoints()
    {
        lock (_waypoints)
        {
            return _waypoints.Values.SelectMany(s => s).ToHashSet();
        }
    }

    public IReadOnlySet<IWaypoint> GetByTag(IWaypoint.Tag tag)
    {
        lock (_waypoints)
        {
            return _waypoints.TryGetValue(tag, out var set) ? set : new HashSet<IWaypoint>();
        }
    }

    public IWaypoint? GetMostRecentByTag(IWaypoint.Tag tag)
    {
        lock (_waypoints)
        {
            if (!_waypoints.TryGetValue(tag, out var set) || set.Count == 0)
            {
                return null;
            }
            return set.OrderByDescending(w => w.GetCreationTimestamp()).FirstOrDefault();
        }
    }

    public void AddWaypoint(IWaypoint waypoint)
    {
        lock (_waypoints)
        {
            if (!_waypoints.TryGetValue(waypoint.GetTag(), out var set))
            {
                set = new HashSet<IWaypoint>();
                _waypoints[waypoint.GetTag()] = set;
            }
            set.Add(waypoint);
            Save(waypoint.GetTag());
        }
    }

    public void RemoveWaypoint(IWaypoint waypoint)
    {
        lock (_waypoints)
        {
            if (!_waypoints.TryGetValue(waypoint.GetTag(), out var set))
            {
                return;
            }
            var removed = set.Remove(waypoint);
            if (removed)
            {
                Save(waypoint.GetTag());
            }
        }
    }
}

