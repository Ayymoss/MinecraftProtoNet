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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Cache;
using MinecraftProtoNet.Baritone.Utils.Pathing;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Wraps get for chunk caching capability.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java
/// </summary>
public class BlockStateInterface
{
    private readonly IPlayerContext _ctx;
    private readonly WorldData? _worldData;
    private readonly Level _world;
    public readonly BetterWorldBorder WorldBorder;

    // Last-accessed chunk cache (Baritone's BlockStateInterface optimization). The A* hot path does hundreds
    // of block lookups per node, almost all within the same chunk; caching the chunk reference skips the
    // chunk-map ConcurrentDictionary lookup for those. Reference: BlockStateInterface.java prev/prevCached.
    private MinecraftProtoNet.Core.Models.World.Chunk.Chunk? _prevChunk;
    private int _prevChunkX = int.MinValue;
    private int _prevChunkZ = int.MinValue;
    private CachedRegion? _prevCached;

    private readonly bool _useTheRealWorld;

    private static readonly BlockState Air = new(0, "minecraft:air");

    public BlockStateInterface(IPlayerContext ctx) : this(ctx, false)
    {
    }

    public BlockStateInterface(IPlayerContext ctx, bool copyLoadedChunks)
    {
        _ctx = ctx;
        _world = (Level)(ctx.World() ?? throw new InvalidOperationException("World cannot be null"));
        WorldBorder = new BetterWorldBorder(_world.WorldBorder);
        _worldData = (WorldData?)ctx.WorldData();
        _useTheRealWorld = !Core.Baritone.Settings().PathThroughCachedOnly.Value;
        // Note: Thread safety check would go here in full implementation
    }

    public bool WorldContainsLoadedChunk(int blockX, int blockZ)
    {
        return _world.HasChunk(blockX >> 4, blockZ >> 4);
    }

    public static BlockState Get(IPlayerContext ctx, BetterBlockPos pos)
    {
        return new BlockStateInterface(ctx).Get0(pos.X, pos.Y, pos.Z);
    }

    public BlockState Get0(int x, int y, int z)
    {
        int adjY = y - _world.DimensionType.MinY;
        // Invalid vertical position
        if (adjY < 0 || adjY >= _world.DimensionType.Height)
        {
            return Air;
        }

        if (_useTheRealWorld)
        {
            // Try the real world first, via the cached chunk when the lookup is in the same chunk as the last.
            int cx = x >> 4;
            int cz = z >> 4;
            if (_prevChunk == null || _prevChunkX != cx || _prevChunkZ != cz)
            {
                _prevChunk = _world.GetChunk(cx, cz);
                _prevChunkX = cx;
                _prevChunkZ = cz;
            }
            if (_prevChunk != null)
            {
                var block = _prevChunk.GetBlock(x, y, z);
                if (block != null)
                {
                    return block;
                }
            }
        }

        // Fall back to cached world
        if (_worldData == null)
        {
            return Air;
        }

        CachedRegion? cached = _prevCached;
        if (cached == null || cached.GetX() != (x >> 9) || cached.GetZ() != (z >> 9))
        {
            var region = _worldData.Cache.GetRegion(x >> 9, z >> 9);
            if (region == null)
            {
                return Air;
            }
            _prevCached = (CachedRegion)region;
            cached = _prevCached;
        }

        var blockState = cached.GetBlock(x & 511, y, z & 511);
        if (blockState == null)
        {
            return Air;
        }
        return blockState as BlockState ?? Air;
    }

    public bool IsLoaded(int x, int z)
    {
        if (_useTheRealWorld)
        {
            if (_world.HasChunk(x >> 4, z >> 4))
            {
                return true;
            }
        }

        CachedRegion? prevRegion = _prevCached;
        if (prevRegion != null && prevRegion.GetX() == (x >> 9) && prevRegion.GetZ() == (z >> 9))
        {
            return prevRegion.IsCached(x & 511, z & 511);
        }

        if (_worldData == null)
        {
            return false;
        }

        var region = _worldData.Cache.GetRegion(x >> 9, z >> 9);
        if (region == null)
        {
            return false;
        }

        _prevCached = (CachedRegion)region;
        return _prevCached.IsCached(x & 511, z & 511);
    }
}

