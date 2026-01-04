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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalBlock.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// A specific BlockPos goal.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalBlock.java
/// </summary>
public class GoalBlock : Goal
{
    /// <summary>
    /// The X block position of this goal.
    /// </summary>
    public readonly int X;

    /// <summary>
    /// The Y block position of this goal.
    /// </summary>
    public readonly int Y;

    /// <summary>
    /// The Z block position of this goal.
    /// </summary>
    public readonly int Z;

    public GoalBlock(BetterBlockPos pos) : this(pos.X, pos.Y, pos.Z)
    {
    }

    public GoalBlock(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override bool IsInGoal(int x, int y, int z)
    {
        return x == X && y == Y && z == Z;
    }

    public override double Heuristic(int x, int y, int z)
    {
        int xDiff = x - X;
        int yDiff = y - Y;
        int zDiff = z - Z;
        return Calculate(xDiff, yDiff, zDiff);
    }

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (obj is not GoalBlock goal)
        {
            return false;
        }
        return X == goal.X && Y == goal.Y && Z == goal.Z;
    }

    public override int GetHashCode()
    {
        return (int)(BetterBlockPos.LongHash(X, Y, Z) * 905165533);
    }

    public override string ToString()
    {
        return $"GoalBlock{{x={X},y={Y},z={Z}}}";
    }

    /// <summary>
    /// Gets the position of this goal as a BetterBlockPos.
    /// </summary>
    public BetterBlockPos GetGoalPos()
    {
        return new BetterBlockPos(X, Y, Z);
    }

    public static double Calculate(double xDiff, int yDiff, double zDiff)
    {
        double heuristic = 0;

        // if yDiff is 1 that means that currentY-goalY==1 which means that we're 1 block above where we should be
        // therefore going from 0,yDiff,0 to a GoalYLevel of 0 is accurate
        heuristic += GoalYLevel.Calculate(0, yDiff);

        // use the pythagorean and manhattan mixture from GoalXZ
        heuristic += GoalXZ.Calculate(xDiff, zDiff);
        return heuristic;
    }
}

