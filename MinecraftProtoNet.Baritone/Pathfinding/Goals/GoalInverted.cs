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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalInverted.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Invert any goal.
/// In the old chat control system, #invert just tried to pick a GoalRunAway that effectively inverted the
/// current goal. This goal just reverses the heuristic to act as a TRUE invert. Inverting a Y level? Baritone tries to
/// get away from that Y level. Inverting a GoalBlock? Baritone will try to make distance whether it's in the X, Y or Z
/// directions. And of course, you can always invert a GoalXZ.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalInverted.java
/// </summary>
public class GoalInverted : Goal
{
    public readonly Goal Origin;

    public GoalInverted(Goal origin)
    {
        Origin = origin;
    }

    public override bool IsInGoal(int x, int y, int z)
    {
        return false;
    }

    public override double Heuristic(int x, int y, int z)
    {
        return -Origin.Heuristic(x, y, z);
    }

    public override double Heuristic()
    {
        return double.NegativeInfinity;
    }

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (obj is not GoalInverted goal)
        {
            return false;
        }
        return Origin.Equals(goal.Origin);
    }

    public override int GetHashCode()
    {
        return Origin.GetHashCode() * 495796690;
    }

    public override string ToString()
    {
        return $"GoalInverted{{{Origin}}}";
    }
}

