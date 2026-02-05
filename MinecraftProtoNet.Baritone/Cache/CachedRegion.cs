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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/CachedRegion.java
 */

using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Cache;

/// <summary>
/// Cached region implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/CachedRegion.java
/// </summary>
public class CachedRegion : ICachedRegion
{
    private const byte ChunkNotPresent = 0;
    private const byte ChunkPresent = 1;
    private const int CachedRegionMagic = 456022911;

    private readonly CachedChunk?[][] _chunks = new CachedChunk[32][];
    private readonly int _x;
    private readonly int _z;
    private readonly int _minY;
    private readonly int _height;
    private readonly bool _hasCeiling;
    private readonly string _dimensionId;

    private bool _hasUnsavedChanges;

    public CachedRegion(int x, int z, int minY, int height, bool hasCeiling, string dimensionId)
    {
        _x = x;
        _z = z;
        _minY = minY;
        _height = height;
        _hasCeiling = hasCeiling;
        _dimensionId = dimensionId;
        _hasUnsavedChanges = false;

        for (int i = 0; i < 32; i++)
        {
            _chunks[i] = new CachedChunk[32];
        }
    }

    public object? GetBlock(int x, int y, int z)
    {
        var adjY = y - _minY;
        var chunk = _chunks[x >> 4][z >> 4];
        if (chunk != null)
        {
            return chunk.GetBlock(x & 15, adjY, z & 15, _minY, _height, _hasCeiling, _dimensionId);
        }
        return null;
    }

    public bool IsCached(int x, int z)
    {
        return _chunks[x >> 4][z >> 4] != null;
    }

    public int GetX() => _x;
    public int GetZ() => _z;

    public List<BetterBlockPos> GetLocationsOf(string block)
    {
        var result = new List<BetterBlockPos>();
        for (int chunkX = 0; chunkX < 32; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < 32; chunkZ++)
            {
                if (_chunks[chunkX][chunkZ] == null)
                {
                    continue;
                }
                var locs = _chunks[chunkX][chunkZ]!.GetAbsoluteBlocks(block);
                if (locs != null)
                {
                    result.AddRange(locs);
                }
            }
        }
        return result;
    }

    public void UpdateCachedChunk(int chunkX, int chunkZ, CachedChunk chunk)
    {
        lock (this)
        {
            _chunks[chunkX][chunkZ] = chunk;
            _hasUnsavedChanges = true;
        }
    }

    public void Save(string directory)
    {
        if (!_hasUnsavedChanges)
        {
            return;
        }

        RemoveExpired();
        // Save implementation will be added when file I/O is integrated
        // For now, just mark as saved
        _hasUnsavedChanges = false;
    }

    public void Load(string directory)
    {
        // Load implementation will be added when file I/O is integrated
    }

    public void RemoveExpired()
    {
        var expiry = Api.BaritoneAPI.GetSettings().CachedChunksExpirySeconds.Value;
        if (expiry < 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var oldestAcceptableAge = now - expiry * 1000L;

        for (int x = 0; x < 32; x++)
        {
            for (int z = 0; z < 32; z++)
            {
                if (_chunks[x][z] != null && _chunks[x][z]!.CacheTimestamp < oldestAcceptableAge)
                {
                    _chunks[x][z] = null;
                }
            }
        }
    }

    public CachedChunk? MostRecentlyModified()
    {
        CachedChunk? recent = null;
        for (int x = 0; x < 32; x++)
        {
            for (int z = 0; z < 32; z++)
            {
                if (_chunks[x][z] == null)
                {
                    continue;
                }
                if (recent == null || _chunks[x][z]!.CacheTimestamp > recent.CacheTimestamp)
                {
                    recent = _chunks[x][z];
                }
            }
        }
        return recent;
    }
}

