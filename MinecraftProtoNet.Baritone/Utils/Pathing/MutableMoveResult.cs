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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/MutableMoveResult.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Movement;

namespace MinecraftProtoNet.Baritone.Utils.Pathing;

/// <summary>
/// The result of a calculated movement, with destination x, y, z, and the cost of performing the movement.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/MutableMoveResult.java
/// </summary>
public sealed class MutableMoveResult
{
    public int X;
    public int Y;
    public int Z;
    public double Cost;

    public MutableMoveResult()
    {
        Reset();
    }

    public void Reset()
    {
        X = 0;
        Y = 0;
        Z = 0;
        Cost = ActionCosts.CostInf;
    }
}

