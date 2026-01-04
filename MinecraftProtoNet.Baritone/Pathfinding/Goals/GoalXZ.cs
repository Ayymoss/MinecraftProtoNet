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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalXZ.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Useful for long-range goals that don't have a specific Y level.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalXZ.java
/// </summary>
public class GoalXZ : Goal
{
    private static readonly double Sqrt2 = Math.Sqrt(2);

    /// <summary>
    /// The X block position of this goal.
    /// </summary>
    private readonly int _x;

    /// <summary>
    /// The Z block position of this goal.
    /// </summary>
    private readonly int _z;

    public GoalXZ(int x, int z)
    {
        _x = x;
        _z = z;
    }

    public GoalXZ(BetterBlockPos pos)
    {
        _x = pos.X;
        _z = pos.Z;
    }

    public override bool IsInGoal(int x, int y, int z)
    {
        return x == _x && z == _z;
    }

    public override double Heuristic(int x, int y, int z)
    {
        int xDiff = x - _x;
        int zDiff = z - _z;
        return Calculate(xDiff, zDiff);
    }

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (obj is not GoalXZ goal)
        {
            return false;
        }
        return _x == goal._x && _z == goal._z;
    }

    public override int GetHashCode()
    {
        int hash = 1791873246;
        hash = hash * 222601791 + _x;
        hash = hash * -1331679453 + _z;
        return hash;
    }

    public override string ToString()
    {
        return $"GoalXZ{{x={_x},z={_z}}}";
    }

    public static double Calculate(double xDiff, double zDiff)
    {
        // This is a combination of pythagorean and manhattan distance
        // It takes into account the fact that pathing can either walk diagonally or forwards

        // It's not possible to walk forward 1 and right 2 in sqrt(5) time
        // It's really 1+sqrt(2) because it'll walk forward 1 then diagonally 1
        double x = Math.Abs(xDiff);
        double z = Math.Abs(zDiff);
        double straight;
        double diagonal;
        if (x < z)
        {
            straight = z - x;
            diagonal = x;
        }
        else
        {
            straight = x - z;
            diagonal = z;
        }
        diagonal *= Sqrt2;
        return (diagonal + straight) * BaritoneAPI.GetSettings().CostHeuristic.Value;
    }

    public int GetX() => _x;
    public int GetZ() => _z;
}

