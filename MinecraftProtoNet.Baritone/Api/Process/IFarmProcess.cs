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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IFarmProcess.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Process;

/// <summary>
/// Interface for farm process.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IFarmProcess.java
/// </summary>
public interface IFarmProcess : IBaritoneProcess
{
    /// <summary>
    /// Begin to search for crops to farm with in specified area from specified location.
    /// </summary>
    void Farm(int range, BetterBlockPos? pos);

    /// <summary>
    /// Begin to search for nearby crops to farm.
    /// </summary>
    void Farm();

    /// <summary>
    /// Begin to search for crops to farm with in specified area from the position the command was executed.
    /// </summary>
    void Farm(int range);
}

