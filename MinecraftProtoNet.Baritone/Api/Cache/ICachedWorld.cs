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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/ICachedWorld.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Cache;

/// <summary>
/// Interface for cached world.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/ICachedWorld.java
/// </summary>
public interface ICachedWorld
{
    /// <summary>
    /// Returns the region at the specified region coordinates.
    /// </summary>
    ICachedRegion? GetRegion(int regionX, int regionZ);

    /// <summary>
    /// Queues the specified chunk for packing.
    /// </summary>
    void QueueForPacking(object chunk); // Will be typed properly when we implement chunk system

    /// <summary>
    /// Returns whether or not the block at the specified X and Z coordinates is cached in this world.
    /// </summary>
    bool IsCached(int blockX, int blockZ);

    /// <summary>
    /// Scans the cached chunks for location of the specified special block.
    /// </summary>
    IReadOnlyList<BetterBlockPos> GetLocationsOf(string block, int maximum, int centerX, int centerZ, int maxRegionDistanceSq);

    /// <summary>
    /// Reloads all of the cached regions in this world from disk.
    /// </summary>
    void ReloadAllFromDisk();

    /// <summary>
    /// Saves all of the cached regions in this world to disk.
    /// </summary>
    void Save();
}

