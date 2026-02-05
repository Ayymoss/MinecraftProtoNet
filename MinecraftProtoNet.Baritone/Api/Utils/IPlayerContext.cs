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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerContext.java
 */

using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Core.Models.Core;

namespace MinecraftProtoNet.Baritone.Api.Utils;

/// <summary>
/// Interface for player context.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerContext.java
/// </summary>
public interface IPlayerContext
{
    /// <summary>
    /// Gets the minecraft client instance.
    /// </summary>
    object Minecraft(); // Will be typed as IMinecraftClient when integrated

    /// <summary>
    /// Gets the player entity.
    /// </summary>
    object? Player(); // Will be typed as Entity when integrated

    /// <summary>
    /// Gets the player controller.
    /// </summary>
    IPlayerController PlayerController();

    /// <summary>
    /// Gets the world/level.
    /// </summary>
    object? World(); // Will be typed as Level when integrated

    /// <summary>
    /// Gets the world data.
    /// </summary>
    IWorldData? WorldData();

    /// <summary>
    /// Gets the object the mouse is over.
    /// </summary>
    object? ObjectMouseOver();

    /// <summary>
    /// Gets the player's feet position.
    /// </summary>
    BetterBlockPos? PlayerFeet();

    /// <summary>
    /// Gets the player's head position.
    /// </summary>
    Vector3<double>? PlayerHead();

    /// <summary>
    /// Gets the player's motion/velocity.
    /// </summary>
    Vector3<double>? PlayerMotion();

    /// <summary>
    /// Gets the viewer position.
    /// </summary>
    BetterBlockPos? ViewerPos();

    /// <summary>
    /// Gets the player's rotations.
    /// </summary>
    Rotation? PlayerRotations();

    /// <summary>
    /// Returns the block that the crosshair is currently placed over.
    /// </summary>
    BetterBlockPos? GetSelectedBlock();

    /// <summary>
    /// Checks if the player is looking at the specified position.
    /// </summary>
    bool IsLookingAt(BetterBlockPos pos);
}

