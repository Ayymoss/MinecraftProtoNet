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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/Goal.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Pathing.Goals;

/// <summary>
/// An abstract Goal for pathing, can be anything from a specific block to just a Y coordinate.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/Goal.java
/// </summary>
public abstract class Goal
{
    /// <summary>
    /// Returns whether or not the specified position meets the requirement for this goal.
    /// </summary>
    public abstract bool IsInGoal(int x, int y, int z);

    /// <summary>
    /// Estimate the number of ticks it will take to get to the goal.
    /// </summary>
    public abstract double Heuristic(int x, int y, int z);

    /// <summary>
    /// Returns whether or not the specified position meets the requirement for this goal.
    /// </summary>
    public bool IsInGoal(BetterBlockPos pos)
    {
        return IsInGoal(pos.X, pos.Y, pos.Z);
    }

    /// <summary>
    /// Estimate the number of ticks it will take to get to the goal.
    /// </summary>
    public double Heuristic(BetterBlockPos pos)
    {
        return Heuristic(pos.X, pos.Y, pos.Z);
    }

    /// <summary>
    /// Returns the heuristic at the goal.
    /// i.e. heuristic() == heuristic(x,y,z) when isInGoal(x,y,z) == true
    /// This is needed by PathingBehavior#estimatedTicksToGoal because
    /// some Goals actually do not have a heuristic of 0 when that condition is met
    /// </summary>
    public virtual double Heuristic()
    {
        return 0;
    }
}

