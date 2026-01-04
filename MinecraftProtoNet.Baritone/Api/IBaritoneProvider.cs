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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/IBaritoneProvider.java
 */

using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Baritone.Api.Command;
using MinecraftProtoNet.Baritone.Api.Schematic;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Core;

namespace MinecraftProtoNet.Baritone.Api;

/// <summary>
/// Interface for Baritone provider.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/IBaritoneProvider.java
/// </summary>
public interface IBaritoneProvider
{
    /// <summary>
    /// Gets the primary Baritone instance.
    /// </summary>
    IBaritone GetPrimaryBaritone();

    /// <summary>
    /// Gets all Baritone instances.
    /// </summary>
    IReadOnlyList<IBaritone> GetAllBaritones();

    /// <summary>
    /// Creates a new Baritone instance for the given Minecraft client.
    /// </summary>
    IBaritone CreateBaritone(IMinecraftClient minecraft);

    /// <summary>
    /// Destroys a Baritone instance.
    /// </summary>
    bool DestroyBaritone(IBaritone baritone);

    /// <summary>
    /// Gets the world scanner.
    /// </summary>
    IWorldScanner? GetWorldScanner();

    /// <summary>
    /// Gets the command system.
    /// </summary>
    ICommandSystem? GetCommandSystem();

    /// <summary>
    /// Gets the schematic system (not supported in headless client).
    /// </summary>
    ISchematicSystem? GetSchematicSystem();
}

