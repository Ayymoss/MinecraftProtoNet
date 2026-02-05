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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/PathingBlockType.java
 */

namespace MinecraftProtoNet.Baritone.Pathfinding;

/// <summary>
/// Pathing block type enum for 2-bit encoding.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/PathingBlockType.java
/// </summary>
public enum PathingBlockType
{
    /// <summary>
    /// Air (0b00)
    /// </summary>
    Air = 0b00,

    /// <summary>
    /// Water (0b01)
    /// </summary>
    Water = 0b01,

    /// <summary>
    /// Avoid (0b10)
    /// </summary>
    Avoid = 0b10,

    /// <summary>
    /// Solid (0b11)
    /// </summary>
    Solid = 0b11
}

/// <summary>
/// Extension methods for PathingBlockType.
/// </summary>
public static class PathingBlockTypeExtensions
{
    /// <summary>
    /// Gets the bits for this pathing block type.
    /// </summary>
    public static (bool Bit1, bool Bit2) GetBits(this PathingBlockType type)
    {
        return type switch
        {
            PathingBlockType.Air => (false, false),
            PathingBlockType.Water => (false, true),
            PathingBlockType.Avoid => (true, false),
            PathingBlockType.Solid => (true, true),
            _ => (false, false)
        };
    }

    /// <summary>
    /// Creates a PathingBlockType from two bits.
    /// </summary>
    public static PathingBlockType FromBits(bool bit1, bool bit2)
    {
        return (bit1, bit2) switch
        {
            (false, false) => PathingBlockType.Air,
            (false, true) => PathingBlockType.Water,
            (true, false) => PathingBlockType.Avoid,
            (true, true) => PathingBlockType.Solid
        };
    }
}

