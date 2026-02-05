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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/precompute/PrecomputedData.java
 */

using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Models.World.Chunk;

namespace MinecraftProtoNet.Baritone.Pathfinding.Precompute;

using static Ternary;

/// <summary>
/// Precomputed data for pathfinding optimizations.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/precompute/PrecomputedData.java
/// </summary>
public class PrecomputedData
{
    // Note: In C#, we can't easily get Block.BLOCK_STATE_REGISTRY.size() at compile time.
    // We'll use a large enough array size or make it dynamic.
    // For now, using a dictionary-based approach which is more flexible.
    private readonly Dictionary<int, int> _data = new();

    private const int CompletedMask = 1 << 0;
    private const int CanWalkOnMask = 1 << 1;
    private const int CanWalkOnSpecialMask = 1 << 2;
    private const int CanWalkThroughMask = 1 << 3;
    private const int CanWalkThroughSpecialMask = 1 << 4;
    private const int FullyPassableMask = 1 << 5;
    private const int FullyPassableSpecialMask = 1 << 6;

    private int FillData(int id, BlockState state)
    {
        int blockData = 0;

        Ternary canWalkOnState = MovementHelper.CanWalkOnBlockState(state);
        if (canWalkOnState == Yes)
        {
            blockData |= CanWalkOnMask;
        }
        if (canWalkOnState == Maybe)
        {
            blockData |= CanWalkOnSpecialMask;
        }

        Ternary canWalkThroughState = MovementHelper.CanWalkThroughBlockState(state);
        if (canWalkThroughState == Yes)
        {
            blockData |= CanWalkThroughMask;
        }
        if (canWalkThroughState == Maybe)
        {
            blockData |= CanWalkThroughSpecialMask;
        }

        Ternary fullyPassableState = MovementHelper.FullyPassableBlockState(state);
        if (fullyPassableState == Yes)
        {
            blockData |= FullyPassableMask;
        }
        if (fullyPassableState == Maybe)
        {
            blockData |= FullyPassableSpecialMask;
        }

        blockData |= CompletedMask;

        _data[id] = blockData; // Thread-safe in theory because every thread should compute the exact same int
        return blockData;
    }

    public bool CanWalkOn(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/precompute/PrecomputedData.java:87-104
        // Use block state ID for caching - combine name and properties hash
        int id = GetBlockStateId(state);
        if (!_data.TryGetValue(id, out int blockData))
        {
            blockData = FillData(id, state);
        }

        if ((blockData & CanWalkOnSpecialMask) != 0)
        {
            return MovementHelper.CanWalkOnPosition(bsi, x, y, z, state);
        }
        else
        {
            return (blockData & CanWalkOnMask) != 0;
        }
    }

    public bool CanWalkThrough(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/precompute/PrecomputedData.java:106-122
        int id = GetBlockStateId(state);
        if (!_data.TryGetValue(id, out int blockData))
        {
            blockData = FillData(id, state);
        }

        if ((blockData & CanWalkThroughSpecialMask) != 0)
        {
            return MovementHelper.CanWalkThroughPosition(bsi, x, y, z, state);
        }
        else
        {
            return (blockData & CanWalkThroughMask) != 0;
        }
    }

    public bool FullyPassable(BlockStateInterface bsi, int x, int y, int z, BlockState state)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/precompute/PrecomputedData.java:124-140
        int id = GetBlockStateId(state);
        if (!_data.TryGetValue(id, out int blockData))
        {
            blockData = FillData(id, state);
        }

        if ((blockData & FullyPassableSpecialMask) != 0)
        {
            return MovementHelper.FullyPassablePosition(bsi, x, y, z, state);
        }
        else
        {
            return (blockData & FullyPassableMask) != 0;
        }
    }

    /// <summary>
    /// Gets a stable ID for a block state.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/precompute/PrecomputedData.java
    /// In Java, this uses Block.BLOCK_STATE_REGISTRY.getId(state).
    /// In C#, BlockState already has an Id property from the registry.
    /// </summary>
    private static int GetBlockStateId(BlockState state)
    {
        // BlockState.Id is the protocol ID from the block state registry
        // This is the same as Java's Block.BLOCK_STATE_REGISTRY.getId(state)
        return state.Id;
    }
}

