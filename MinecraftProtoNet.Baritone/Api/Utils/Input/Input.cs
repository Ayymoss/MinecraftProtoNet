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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/input/Input.java
 */

namespace MinecraftProtoNet.Baritone.Api.Utils.Input;

/// <summary>
/// Enum representing the inputs that control the player's behavior.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/input/Input.java
/// </summary>
public enum Input
{
    /// <summary>
    /// The move forward input
    /// </summary>
    MoveForward,

    /// <summary>
    /// The move back input
    /// </summary>
    MoveBack,

    /// <summary>
    /// The move left input
    /// </summary>
    MoveLeft,

    /// <summary>
    /// The move right input
    /// </summary>
    MoveRight,

    /// <summary>
    /// The attack input
    /// </summary>
    ClickLeft,

    /// <summary>
    /// The use item input
    /// </summary>
    ClickRight,

    /// <summary>
    /// The jump input
    /// </summary>
    Jump,

    /// <summary>
    /// The sneak input
    /// </summary>
    Sneak,

    /// <summary>
    /// The sprint input
    /// </summary>
    Sprint
}

