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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/IBlockTypeAccess.java
 */

namespace MinecraftProtoNet.Baritone.Api.Cache;

/// <summary>
/// Interface for block type access.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/cache/IBlockTypeAccess.java
/// </summary>
public interface IBlockTypeAccess
{
    /// <summary>
    /// Gets the block state at the specified coordinates.
    /// </summary>
    object? GetBlock(int x, int y, int z); // Will be typed as BlockState when we integrate with Core
}

