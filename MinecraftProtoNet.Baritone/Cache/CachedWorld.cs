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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/CachedWorld.java
 */

using System.Collections.Concurrent;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Cache;

/// <summary>
/// Cached world implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/CachedWorld.java
/// </summary>
public class CachedWorld : ICachedWorld
{
    private const int RegionMax = 30_000_000 / 512 + 1;

    private readonly Dictionary<long, CachedRegion> _cachedRegions = new();
    private readonly string _directory;
    private readonly int _minY;
    private readonly int _height;
    private readonly bool _hasCeiling;
    private readonly string _dimensionId;

    private readonly ConcurrentQueue<(int ChunkX, int ChunkZ)> _toPackQueue = new();
    private readonly ConcurrentDictionary<(int ChunkX, int ChunkZ), object> _toPackMap = new(); // Chunk will be typed when integrated

    public CachedWorld(string directory, int minY, int height, bool hasCeiling, string dimensionId)
    {
        _directory = directory;
        _minY = minY;
        _height = height;
        _hasCeiling = hasCeiling;
        _dimensionId = dimensionId;

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

        // Start packer thread
        Task.Run(PackerThread);
        
        // Start save thread
        Task.Run(async () =>
        {
            await Task.Delay(30000); // Wait 30 seconds
            while (true)
            {
                Save();
                await Task.Delay(600000); // 10 minutes
            }
        });
    }

    public void QueueForPacking(object chunk)
    {
        // Will be properly typed when integrated with Core
        // For now, extract chunk position from chunk object
        // _toPackMap.TryAdd((chunkX, chunkZ), chunk);
        // _toPackQueue.Enqueue((chunkX, chunkZ));
    }

    public bool IsCached(int blockX, int blockZ)
    {
        var region = GetRegion(blockX >> 9, blockZ >> 9);
        if (region == null)
        {
            return false;
        }
        return region.IsCached(blockX & 511, blockZ & 511);
    }

    public IReadOnlyList<BetterBlockPos> GetLocationsOf(string block, int maximum, int centerX, int centerZ, int maxRegionDistanceSq)
    {
        var result = new List<BetterBlockPos>();
        var centerRegionX = centerX >> 9;
        var centerRegionZ = centerZ >> 9;

        int searchRadius = 0;
        while (searchRadius <= maxRegionDistanceSq)
        {
            for (int xoff = -searchRadius; xoff <= searchRadius; xoff++)
            {
                for (int zoff = -searchRadius; zoff <= searchRadius; zoff++)
                {
                    var distance = xoff * xoff + zoff * zoff;
                    if (distance != searchRadius)
                    {
                        continue;
                    }
                    var regionX = xoff + centerRegionX;
                    var regionZ = zoff + centerRegionZ;
                    var region = GetOrCreateRegion(regionX, regionZ);
                    if (region != null)
                    {
                        result.AddRange(region.GetLocationsOf(block));
                    }
                    if (result.Count >= maximum)
                    {
                        return result;
                    }
                }
            }
            searchRadius++;
        }
        return result;
    }

    public void ReloadAllFromDisk()
    {
        lock (_cachedRegions)
        {
            foreach (var region in _cachedRegions.Values)
            {
                region?.Load(_directory);
            }
        }
    }

    public void Save()
    {
        if (!MinecraftProtoNet.Baritone.Core.Baritone.Settings().ChunkCaching.Value)
        {
            lock (_cachedRegions)
            {
                foreach (var region in _cachedRegions.Values)
                {
                    region?.RemoveExpired();
                }
            }
            Prune();
            return;
        }

        lock (_cachedRegions)
        {
            var regions = _cachedRegions.Values.ToList();
            Parallel.ForEach(regions, region =>
            {
                region?.Save(_directory);
            });
        }
        Prune();
    }

    private void Prune()
    {
        if (!MinecraftProtoNet.Baritone.Core.Baritone.Settings().PruneRegionsFromRam.Value)
        {
            return;
        }

        var pruneCenter = GuessPosition();
        lock (_cachedRegions)
        {
            var regionsToRemove = new List<long>();
            foreach (var kvp in _cachedRegions)
            {
                var region = kvp.Value;
                if (region == null) continue;

                var distX = ((region.GetX() << 9) + 256) - pruneCenter.X;
                var distZ = ((region.GetZ() << 9) + 256) - pruneCenter.Z;
                var dist = Math.Sqrt(distX * distX + distZ * distZ);
                if (dist > 1024)
                {
                    regionsToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in regionsToRemove)
            {
                _cachedRegions.Remove(key);
            }
        }
    }

    private BetterBlockPos GuessPosition()
    {
        // Try to get player position from any Baritone instance
        foreach (var baritone in BaritoneAPI.GetProvider().GetAllBaritones())
        {
            var data = baritone.GetWorldProvider().GetCurrentWorld();
            if (data != null && data.GetCachedWorld() == this)
            {
                var playerFeet = baritone.GetPlayerContext().PlayerFeet();
                return playerFeet ?? new BetterBlockPos(0, 0, 0);
            }
        }

        // Otherwise, use most recently modified chunk
        CachedChunk? mostRecentlyModified = null;
        lock (_cachedRegions)
        {
            foreach (var region in _cachedRegions.Values)
            {
                if (region == null) continue;
                var ch = region.MostRecentlyModified();
                if (ch == null) continue;
                if (mostRecentlyModified == null || mostRecentlyModified.CacheTimestamp < ch.CacheTimestamp)
                {
                    mostRecentlyModified = ch;
                }
            }
        }

        if (mostRecentlyModified == null)
        {
            return new BetterBlockPos(0, 0, 0);
        }

        return new BetterBlockPos((mostRecentlyModified.X << 4) + 8, 0, (mostRecentlyModified.Z << 4) + 8);
    }

    public ICachedRegion? GetRegion(int regionX, int regionZ)
    {
        lock (_cachedRegions)
        {
            var id = GetRegionId(regionX, regionZ);
            return _cachedRegions.TryGetValue(id, out var region) ? region : null;
        }
    }

    private CachedRegion? GetOrCreateRegion(int regionX, int regionZ)
    {
        lock (_cachedRegions)
        {
            var id = GetRegionId(regionX, regionZ);
            if (_cachedRegions.TryGetValue(id, out var region))
            {
                return region;
            }

            var newRegion = new CachedRegion(regionX, regionZ, _minY, _height, _hasCeiling, _dimensionId);
            newRegion.Load(_directory);
            _cachedRegions[id] = newRegion;
            return newRegion;
        }
    }

    private void UpdateCachedChunk(CachedChunk chunk)
    {
        var region = GetOrCreateRegion(chunk.X >> 5, chunk.Z >> 5);
        region?.UpdateCachedChunk(chunk.X & 31, chunk.Z & 31, chunk);
    }

    private long GetRegionId(int regionX, int regionZ)
    {
        if (!IsRegionInWorld(regionX, regionZ))
        {
            return 0;
        }

        return ((long)regionX & 0xFFFFFFFFL) | ((long)regionZ & 0xFFFFFFFFL) << 32;
    }

    private bool IsRegionInWorld(int regionX, int regionZ)
    {
        return regionX <= RegionMax && regionX >= -RegionMax && regionZ <= RegionMax && regionZ >= -RegionMax;
    }

    private void PackerThread()
    {
        while (true)
        {
            try
            {
                if (_toPackQueue.TryDequeue(out var pos))
                {
                    if (_toPackQueue.Count > MinecraftProtoNet.Baritone.Core.Baritone.Settings().ChunkPackerQueueMaxSize.Value)
                    {
                        continue;
                    }

                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/CachedWorld.java:301
                    // Chunk packing will be implemented when ChunkPacker is fully ported
                    // TODO: Implement chunk packing when ChunkPacker system is available
                    // var chunk = _toPackMap.TryRemove(pos, out var chunkObj) ? chunkObj : null;
                    // if (chunk != null)
                    // {
                    //     var cached = ChunkPacker.Pack(chunk);
                    //     UpdateCachedChunk(cached);
                    // }
                }
                else
                {
                    Thread.Sleep(100); // Wait a bit if queue is empty
                }
            }
            catch (Exception ex)
            {
                // Keep consuming from queue to avoid memory leaks
                Console.WriteLine($"Error in packer thread: {ex}");
            }
        }
    }
}

