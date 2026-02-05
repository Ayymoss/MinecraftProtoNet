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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalTwoBlocks.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Useful if the goal is just to mine a block. This goal will be satisfied if the specified
/// position is at to or above the specified position for this goal.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalTwoBlocks.java
/// </summary>
public class GoalTwoBlocks : Goal
{
    /// <summary>
    /// The X block position of this goal.
    /// </summary>
    protected readonly int X;

    /// <summary>
    /// The Y block position of this goal.
    /// </summary>
    protected readonly int Y;

    /// <summary>
    /// The Z block position of this goal.
    /// </summary>
    protected readonly int Z;

    public GoalTwoBlocks(BetterBlockPos pos) : this(pos.X, pos.Y, pos.Z)
    {
    }

    public GoalTwoBlocks(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override bool IsInGoal(int x, int y, int z)
    {
        return x == X && (y == Y || y == Y - 1) && z == Z;
    }

    public override double Heuristic(int x, int y, int z)
    {
        int xDiff = x - X;
        int yDiff = y - Y;
        int zDiff = z - Z;
        return GoalBlock.Calculate(xDiff, yDiff < 0 ? yDiff + 1 : yDiff, zDiff);
    }

    public BetterBlockPos GetGoalPos()
    {
        return new BetterBlockPos(X, Y, Z);
    }

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (obj is not GoalTwoBlocks goal)
        {
            return false;
        }
        return X == goal.X && Y == goal.Y && Z == goal.Z;
    }

    public override int GetHashCode()
    {
        return (int)(BetterBlockPos.LongHash(X, Y, Z) * 516508351);
    }

    public override string ToString()
    {
        return $"GoalTwoBlocks{{x={X},y={Y},z={Z}}}";
    }
}

