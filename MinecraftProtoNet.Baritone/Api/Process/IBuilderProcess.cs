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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IBuilderProcess.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core.Models.Json;

namespace MinecraftProtoNet.Baritone.Api.Process;

/// <summary>
/// Interface for builder process.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IBuilderProcess.java
/// </summary>
public interface IBuilderProcess : IBaritoneProcess
{
    /// <summary>
    /// Returns the block state that should be placed at the specified position.
    /// </summary>
    BlockState? PlaceAt(int x, int y, int z, BlockState current);

    /// <summary>
    /// Checks if placement at the specified position is plausible.
    /// </summary>
    bool PlacementPlausible(BetterBlockPos pos, BlockState state);
}

