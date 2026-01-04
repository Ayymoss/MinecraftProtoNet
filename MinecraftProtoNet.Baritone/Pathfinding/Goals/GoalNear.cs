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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalNear.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Goal that is satisfied when within a certain range of a position.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalNear.java
/// </summary>
public class GoalNear : Goal
{
    private readonly int _x;
    private readonly int _y;
    private readonly int _z;
    private readonly int _rangeSq;

    public GoalNear(BetterBlockPos pos, int range)
    {
        _x = pos.X;
        _y = pos.Y;
        _z = pos.Z;
        _rangeSq = range * range;
    }

    public GoalNear(int x, int y, int z, int range)
    {
        _x = x;
        _y = y;
        _z = z;
        _rangeSq = range * range;
    }

    public override bool IsInGoal(int x, int y, int z)
    {
        int xDiff = x - _x;
        int yDiff = y - _y;
        int zDiff = z - _z;
        return xDiff * xDiff + yDiff * yDiff + zDiff * zDiff <= _rangeSq;
    }

    public override double Heuristic(int x, int y, int z)
    {
        int xDiff = x - _x;
        int yDiff = y - _y;
        int zDiff = z - _z;
        return GoalBlock.Calculate(xDiff, yDiff, zDiff);
    }

    public override double Heuristic()
    {
        // TODO less hacky solution
        int range = (int)Math.Ceiling(Math.Sqrt(_rangeSq));
        var maybeAlwaysInside = new HashSet<double>(); // see pull request #1978
        double minOutside = double.PositiveInfinity;
        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                for (int dz = -range; dz <= range; dz++)
                {
                    double h = Heuristic(_x + dx, _y + dy, _z + dz);
                    if (h < minOutside && IsInGoal(_x + dx, _y + dy, _z + dz))
                    {
                        maybeAlwaysInside.Add(h);
                    }
                    else
                    {
                        minOutside = Math.Min(minOutside, h);
                    }
                }
            }
        }
        double maxInside = double.NegativeInfinity;
        foreach (double inside in maybeAlwaysInside)
        {
            if (inside < minOutside)
            {
                maxInside = Math.Max(maxInside, inside);
            }
        }
        return maxInside;
    }

    public BetterBlockPos GetGoalPos()
    {
        return new BetterBlockPos(_x, _y, _z);
    }

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (obj is not GoalNear goal)
        {
            return false;
        }
        return _x == goal._x && _y == goal._y && _z == goal._z && _rangeSq == goal._rangeSq;
    }

    public override int GetHashCode()
    {
        return (int)BetterBlockPos.LongHash(_x, _y, _z) + _rangeSq;
    }

    public override string ToString()
    {
        return $"GoalNear{{x={_x}, y={_y}, z={_z}, rangeSq={_rangeSq}}}";
    }
}

