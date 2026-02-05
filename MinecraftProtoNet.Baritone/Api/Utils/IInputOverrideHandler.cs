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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IInputOverrideHandler.java
 */

using MinecraftProtoNet.Baritone.Api.Behavior;

namespace MinecraftProtoNet.Baritone.Api.Utils;

/// <summary>
/// Interface for input override handler.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IInputOverrideHandler.java
/// </summary>
public interface IInputOverrideHandler : IBehavior
{
    /// <summary>
    /// Checks if an input is forced down.
    /// </summary>
    bool IsInputForcedDown(Input.Input input);

    /// <summary>
    /// Sets the force state for an input.
    /// </summary>
    void SetInputForceState(Input.Input input, bool forced);

    /// <summary>
    /// Clears all forced keys.
    /// </summary>
    void ClearAllKeys();
}

