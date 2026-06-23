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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/BlockOptionalMetaLookup.java
 */

using MinecraftProtoNet.Core.Models.World.Chunk;

namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Block optional meta lookup implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/BlockOptionalMetaLookup.java
/// </summary>
public class BlockOptionalMetaLookup
{
    private readonly string[] _blockNames;

    public BlockOptionalMetaLookup(params string[] blockNames)
    {
        // Reference: BlockOptionalMetaLookup.java:34-42 - in Java each entry is a Block; matching is by
        // block identity. We store block names and match exactly (NOT substring — "stone" must not match
        // cobblestone/redstone). Per-state metadata (BlockOptionalMeta) is not modelled here.
        _blockNames = blockNames;
    }


    public bool Has(string blockName)
    {
        return _blockNames.Any(name => name.Equals(blockName, StringComparison.OrdinalIgnoreCase));
    }

    public bool Has(BlockState state)
    {
        return state != null && _blockNames.Any(name => state.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public bool Has(object stack)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/BlockOptionalMetaLookup.java:57-59
        // Check if item stack matches
        // In Java, this checks if the item stack's block matches any of the block names
        // For now, we'll check if the stack is a BlockState and use Has(BlockState)
        if (stack is BlockState state)
        {
            return Has(state);
        }
        // TODO: When item stack system is available, extract block name from item stack and check
        return false;
    }

    public IEnumerable<string> Blocks()
    {
        return _blockNames;
    }

    public override string ToString()
    {
        return string.Join(", ", _blockNames);
    }
}

