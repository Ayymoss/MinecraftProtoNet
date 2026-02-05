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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/behavior/IInventoryBehavior.java
 */

namespace MinecraftProtoNet.Baritone.Api.Behavior;

/// <summary>
/// Interface for inventory behavior.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/behavior/IInventoryBehavior.java
/// </summary>
public interface IInventoryBehavior : IBehavior
{
    /// <summary>
    /// Checks if the inventory has generic throwaway items.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:162
    /// </summary>
    bool HasGenericThrowaway();

    /// <summary>
    /// Selects a throwaway item for the specified location.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java:171-185
    /// </summary>
    bool SelectThrowawayForLocation(bool select, int x, int y, int z);
}

