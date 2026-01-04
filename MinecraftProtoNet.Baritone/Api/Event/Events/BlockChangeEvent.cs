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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/BlockChangeEvent.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Event.Events;

/// <summary>
/// Block change event.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/BlockChangeEvent.java
/// </summary>
public class BlockChangeEvent
{
    private readonly (int ChunkX, int ChunkZ) _chunk;
    private readonly IReadOnlyList<(BetterBlockPos Pos, object BlockState)> _blocks; // BlockState will be typed when integrated

    public BlockChangeEvent((int ChunkX, int ChunkZ) chunk, IReadOnlyList<(BetterBlockPos Pos, object BlockState)> blocks)
    {
        _chunk = chunk;
        _blocks = blocks;
    }

    public (int ChunkX, int ChunkZ) GetChunkPos() => _chunk;
    public IReadOnlyList<(BetterBlockPos Pos, object BlockState)> GetBlocks() => _blocks;
}

