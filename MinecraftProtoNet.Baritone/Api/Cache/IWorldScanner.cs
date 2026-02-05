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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/IWorldScanner.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Cache;

/// <summary>
/// Interface for world scanner.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/IWorldScanner.java
/// </summary>
public interface IWorldScanner
{
    /// <summary>
    /// Scans the world, up to the specified max chunk radius, for the specified blocks.
    /// </summary>
    IReadOnlyList<BetterBlockPos> ScanChunkRadius(IPlayerContext ctx, object filter, int max, int yLevelThreshold, int maxSearchRadius);

    /// <summary>
    /// Scans a single chunk for the specified blocks.
    /// </summary>
    IReadOnlyList<BetterBlockPos> ScanChunk(IPlayerContext ctx, object filter, (int X, int Z) chunkPos, int max, int yLevelThreshold);

    /// <summary>
    /// Queues the chunks in a square formation around the specified player, using the specified
    /// range, which represents 1/2 the square's dimensions, where the player is in the center.
    /// </summary>
    int Repack(IPlayerContext ctx, int range);

    /// <summary>
    /// Overload of Repack where the value of the range parameter is 40.
    /// </summary>
    int Repack(IPlayerContext ctx);
}

