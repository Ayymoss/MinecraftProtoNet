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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/FasterWorldScanner.java
 */

using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Cache;

/// <summary>
/// Faster world scanner implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/FasterWorldScanner.java
/// </summary>
public class FasterWorldScanner : IWorldScanner
{
    public static readonly FasterWorldScanner Instance = new();

    private FasterWorldScanner()
    {
    }

    public IReadOnlyList<BetterBlockPos> ScanChunkRadius(IPlayerContext ctx, object filter, int max, int yLevelThreshold, int maxSearchRadius)
    {
        return Array.Empty<BetterBlockPos>();
    }

    public IReadOnlyList<BetterBlockPos> ScanChunk(IPlayerContext ctx, object filter, (int X, int Z) chunkPos, int max, int yLevelThreshold)
    {
        return Array.Empty<BetterBlockPos>();
    }

    public int Repack(IPlayerContext ctx, int range)
    {
        return 0;
    }

    public int Repack(IPlayerContext ctx)
    {
        return Repack(ctx, 40);
    }
}

