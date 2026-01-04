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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/BetterWorldBorder.java
 */

using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Utils.Pathing;

/// <summary>
/// Essentially, a "rule" for the path finder, prevents proposed movements from attempting to venture
/// into the world border, and prevents actual movements from placing blocks in the world border.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/BetterWorldBorder.java
/// </summary>
public class BetterWorldBorder
{
    private readonly double _minX;
    private readonly double _maxX;
    private readonly double _minZ;
    private readonly double _maxZ;

    public BetterWorldBorder(WorldBorder border)
    {
        _minX = border.MinX;
        _maxX = border.MaxX;
        _minZ = border.MinZ;
        _maxZ = border.MaxZ;
    }

    public bool EntirelyContains(int x, int z)
    {
        return x + 1 > _minX && x < _maxX && z + 1 > _minZ && z < _maxZ;
    }

    public bool CanPlaceAt(int x, int z)
    {
        // move it in 1 block on all sides
        // because we can't place a block at the very edge against a block outside the border
        // it won't let us right click it
        return x > _minX && x + 1 < _maxX && z > _minZ && z + 1 < _maxZ;
    }
}

