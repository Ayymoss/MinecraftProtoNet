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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalComposite.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// A composite of many goals, any one of which satisfies the composite.
/// For example, a GoalComposite of block goals for every oak log in loaded chunks
/// would result in it pathing to the easiest oak log to get to.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalComposite.java
/// </summary>
public class GoalComposite : Goal
{
    /// <summary>
    /// An array of goals that any one of must be satisfied.
    /// </summary>
    private readonly Goal[] _goals;

    public GoalComposite(params Goal[] goals)
    {
        _goals = goals;
    }

    public override bool IsInGoal(int x, int y, int z)
    {
        foreach (var goal in _goals)
        {
            if (goal.IsInGoal(x, y, z))
            {
                return true;
            }
        }
        return false;
    }

    public override double Heuristic(int x, int y, int z)
    {
        double min = double.MaxValue;
        foreach (var g in _goals)
        {
            // TODO technically this isn't admissible...?
            min = Math.Min(min, g.Heuristic(x, y, z)); // whichever is closest
        }
        return min;
    }

    public override double Heuristic()
    {
        double min = double.MaxValue;
        foreach (var g in _goals)
        {
            // just take the highest value that is guaranteed to be inside the goal
            min = Math.Min(min, g.Heuristic());
        }
        return min;
    }

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (obj is not GoalComposite goal)
        {
            return false;
        }
        return _goals.SequenceEqual(goal._goals);
    }

    public override int GetHashCode()
    {
        return _goals.Aggregate(0, (hash, goal) => hash * 31 + goal.GetHashCode());
    }

    public override string ToString()
    {
        return "GoalComposite" + string.Join(", ", _goals.Select(g => g.ToString()));
    }

    public Goal[] Goals() => _goals;
}

