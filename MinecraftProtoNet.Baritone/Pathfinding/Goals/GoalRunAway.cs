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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalRunAway.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Useful for automated combat (retreating specifically).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalRunAway.java
/// </summary>
public class GoalRunAway : Goal
{
    private readonly BetterBlockPos[] _from;
    private readonly int _distanceSq;
    private readonly int? _maintainY;

    public GoalRunAway(double distance, params BetterBlockPos[] from)
        : this(distance, null, from)
    {
    }

    public GoalRunAway(double distance, int? maintainY, params BetterBlockPos[] from)
    {
        if (from.Length == 0)
        {
            throw new ArgumentException("Positions to run away from must not be empty");
        }
        _from = from;
        _distanceSq = (int)(distance * distance);
        _maintainY = maintainY;
    }

    public override bool IsInGoal(int x, int y, int z)
    {
        if (_maintainY != null && _maintainY != y)
        {
            return false;
        }
        foreach (var p in _from)
        {
            int diffX = x - p.X;
            int diffZ = z - p.Z;
            int distSq = diffX * diffX + diffZ * diffZ;
            if (distSq < _distanceSq)
            {
                return false;
            }
        }
        return true;
    }

    public override double Heuristic(int x, int y, int z)
    {
        double min = double.MaxValue;
        foreach (var p in _from)
        {
            double h = GoalXZ.Calculate(p.X - x, p.Z - z);
            if (h < min)
            {
                min = h;
            }
        }
        min = -min;
        if (_maintainY != null)
        {
            min = min * 0.6 + GoalYLevel.Calculate(_maintainY.Value, y) * 1.5;
        }
        return min;
    }

    public override double Heuristic()
    {
        // TODO less hacky solution
        int distance = (int)Math.Ceiling(Math.Sqrt(_distanceSq));
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int minZ = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        int maxZ = int.MinValue;
        foreach (var p in _from)
        {
            minX = Math.Min(minX, p.X - distance);
            minY = Math.Min(minY, p.Y - distance);
            minZ = Math.Min(minZ, p.Z - distance);
            maxX = Math.Max(maxX, p.X + distance);
            maxY = Math.Max(maxY, p.Y + distance);
            maxZ = Math.Max(maxZ, p.Z + distance);
        }
        var maybeAlwaysInside = new HashSet<double>(); // see pull request #1978
        double minOutside = double.PositiveInfinity;
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    double h = Heuristic(x, y, z);
                    if (h < minOutside && IsInGoal(x, y, z))
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

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (obj is not GoalRunAway goal)
        {
            return false;
        }
        return _distanceSq == goal._distanceSq
                && _from.SequenceEqual(goal._from)
                && _maintainY == goal._maintainY;
    }

    public override int GetHashCode()
    {
        int hash = _from.Aggregate(0, (h, pos) => h * 31 + pos.GetHashCode());
        hash = hash * 1196803141 + _distanceSq;
        hash = hash * -2053788840 + (_maintainY?.GetHashCode() ?? 0);
        return hash;
    }

    public override string ToString()
    {
        if (_maintainY != null)
        {
            return $"GoalRunAwayFromMaintainY y={_maintainY}, [{string.Join(", ", _from)}]";
        }
        else
        {
            return $"GoalRunAwayFrom[{string.Join(", ", _from)}]";
        }
    }
}

