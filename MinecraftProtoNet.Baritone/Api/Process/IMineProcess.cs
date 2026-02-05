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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IMineProcess.java
 */

namespace MinecraftProtoNet.Baritone.Api.Process;

/// <summary>
/// Interface for mine process.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IMineProcess.java
/// </summary>
public interface IMineProcess : IBaritoneProcess
{
    /// <summary>
    /// Begin to search for and mine the specified blocks until the number of specified items to get from the blocks that are mined.
    /// </summary>
    void MineByName(int quantity, params string[] blocks);

    /// <summary>
    /// Begin to search for and mine the specified blocks.
    /// </summary>
    void MineByName(params string[] blocks);

    /// <summary>
    /// Cancels the current mining task.
    /// </summary>
    void Cancel();
}

