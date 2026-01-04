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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IBaritoneProcess.java
 */

namespace MinecraftProtoNet.Baritone.Api.Process;

/// <summary>
/// A process that can control the PathingBehavior.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/process/IBaritoneProcess.java
/// </summary>
public interface IBaritoneProcess
{
    /// <summary>
    /// Default priority. Most normal processes should have this value.
    /// </summary>
    const double DefaultPriority = -1;

    /// <summary>
    /// Would this process like to be in control?
    /// </summary>
    bool IsActive();

    /// <summary>
    /// Called when this process is in control of pathing; Returns what Baritone should do.
    /// </summary>
    /// <param name="calcFailed">True if this specific process was in control last tick, and there was a CALC_FAILED event last tick</param>
    /// <param name="isSafeToCancel">True if a REQUEST_PAUSE would happen this tick, and PathingBehavior wouldn't actually tick</param>
    /// <returns>What the PathingBehavior should do</returns>
    PathingCommand OnTick(bool calcFailed, bool isSafeToCancel);

    /// <summary>
    /// Returns whether or not this process should be treated as "temporary".
    /// </summary>
    bool IsTemporary();

    /// <summary>
    /// Called if IsActive returned true, but another non-temporary process has control.
    /// </summary>
    void OnLostControl();

    /// <summary>
    /// Used to determine which Process gains control if multiple are reporting IsActive.
    /// </summary>
    double Priority();

    /// <summary>
    /// Returns a user-friendly name for this process. Suitable for a HUD.
    /// </summary>
    string DisplayName();
}

