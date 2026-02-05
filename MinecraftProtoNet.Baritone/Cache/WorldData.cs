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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/WorldData.java
 */

using MinecraftProtoNet.Baritone.Api.Cache;

namespace MinecraftProtoNet.Baritone.Cache;

/// <summary>
/// World data implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/WorldData.java
/// </summary>
public class WorldData : IWorldData
{
    public readonly CachedWorld Cache;
    private readonly WaypointCollection _waypoints;
    public readonly string Directory;
    public readonly int MinY;
    public readonly int Height;
    public readonly bool HasCeiling;
    public readonly string DimensionId;

    public WorldData(string directory, int minY, int height, bool hasCeiling, string dimensionId)
    {
        Directory = directory;
        MinY = minY;
        Height = height;
        HasCeiling = hasCeiling;
        DimensionId = dimensionId;
        Cache = new CachedWorld(Path.Combine(directory, "cache"), minY, height, hasCeiling, dimensionId);
        _waypoints = new WaypointCollection(Path.Combine(directory, "waypoints"));
    }

    public void OnClose()
    {
        Task.Run(() =>
        {
            Console.WriteLine("Started saving the world in a new thread");
            Cache.Save();
        });
    }

    public ICachedWorld GetCachedWorld() => Cache;

    ICachedWorld IWorldData.Cache => Cache;

    public IWaypointCollection GetWaypoints() => _waypoints;
}

