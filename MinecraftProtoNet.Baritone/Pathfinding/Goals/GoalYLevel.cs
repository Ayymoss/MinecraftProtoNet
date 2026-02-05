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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalYLevel.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Useful for mining (getting to diamond / iron level).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalYLevel.java
/// </summary>
public class GoalYLevel : Goal
{
    /// <summary>
    /// The target Y level.
    /// </summary>
    public readonly int Level;

    public GoalYLevel(int level)
    {
        Level = level;
    }

    public override bool IsInGoal(int x, int y, int z)
    {
        return y == Level;
    }

    public override double Heuristic(int x, int y, int z)
    {
        return Calculate(Level, y);
    }

    public static double Calculate(int goalY, int currentY)
    {
        if (currentY > goalY)
        {
            // need to descend
            return ActionCosts.FallNBlocksCost[2] / 2 * (currentY - goalY);
        }
        if (currentY < goalY)
        {
            // need to ascend
            return (goalY - currentY) * ActionCosts.JumpOneBlockCost;
        }
        return 0;
    }

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (obj is not GoalYLevel goal)
        {
            return false;
        }
        return Level == goal.Level;
    }

    public override int GetHashCode()
    {
        return Level * 1271009915;
    }

    public override string ToString()
    {
        return $"GoalYLevel{{y={Level}}}";
    }
}

