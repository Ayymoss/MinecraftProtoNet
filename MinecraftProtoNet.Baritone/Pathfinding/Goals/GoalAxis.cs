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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalAxis.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Goal for pathing along the axis (X=0 or Z=0 or |X|=|Z|).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalAxis.java
/// </summary>
public class GoalAxis : Goal
{
    private static readonly double Sqrt2Over2 = Math.Sqrt(2) / 2;

    public override bool IsInGoal(int x, int y, int z)
    {
        return y == BaritoneAPI.GetSettings().AxisHeight.Value && (x == 0 || z == 0 || Math.Abs(x) == Math.Abs(z));
    }

    public override double Heuristic(int x0, int y, int z0)
    {
        int x = Math.Abs(x0);
        int z = Math.Abs(z0);

        int shrt = Math.Min(x, z);
        int lng = Math.Max(x, z);
        int diff = lng - shrt;

        double flatAxisDistance = Math.Min(x, Math.Min(z, (int)(diff * Sqrt2Over2)));

        return flatAxisDistance * BaritoneAPI.GetSettings().CostHeuristic.Value + GoalYLevel.Calculate(BaritoneAPI.GetSettings().AxisHeight.Value, y);
    }

    public override bool Equals(object? obj)
    {
        return obj?.GetType() == typeof(GoalAxis);
    }

    public override int GetHashCode()
    {
        return 201385781;
    }

    public override string ToString()
    {
        return "GoalAxis";
    }
}

