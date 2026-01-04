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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/behavior/look/IAimProcessor.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Behavior.Look;

/// <summary>
/// Interface for aim processor.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/behavior/look/IAimProcessor.java
/// </summary>
public interface IAimProcessor
{
    /// <summary>
    /// Returns the actual rotation that will be used when the desired rotation is requested.
    /// </summary>
    Rotation PeekRotation(Rotation desired);

    /// <summary>
    /// Returns a copy of this processor which has its own internal state and is manually tickable.
    /// </summary>
    ITickableAimProcessor Fork();
}

