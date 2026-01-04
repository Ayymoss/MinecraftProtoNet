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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/movement/IMovement.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Pathing.Movement;

/// <summary>
/// Interface for a movement.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/movement/IMovement.java
/// </summary>
public interface IMovement
{
    /// <summary>
    /// Gets the cost of this movement.
    /// </summary>
    double GetCost();

    /// <summary>
    /// Updates the movement state.
    /// </summary>
    MovementStatus Update();

    /// <summary>
    /// Resets the current state status to PREPPING.
    /// </summary>
    void Reset();

    /// <summary>
    /// Resets the cache for special break, place, and walk into blocks.
    /// </summary>
    void ResetBlockCache();

    /// <summary>
    /// Returns whether it is safe to cancel the current movement state.
    /// </summary>
    bool SafeToCancel();

    /// <summary>
    /// Returns whether this movement was calculated while the chunk was loaded.
    /// </summary>
    bool CalculatedWhileLoaded();

    /// <summary>
    /// Gets the source position.
    /// </summary>
    BetterBlockPos GetSrc();

    /// <summary>
    /// Gets the destination position.
    /// </summary>
    BetterBlockPos GetDest();

    /// <summary>
    /// Gets the direction of this movement.
    /// </summary>
    (int X, int Y, int Z) GetDirection();
}

