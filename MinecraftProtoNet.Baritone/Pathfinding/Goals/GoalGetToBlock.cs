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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalGetToBlock.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Don't get into the block, but get directly adjacent to it. Useful for chests.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalGetToBlock.java
/// </summary>
public class GoalGetToBlock : Goal
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    public GoalGetToBlock(BetterBlockPos pos)
    {
        X = pos.X;
        Y = pos.Y;
        Z = pos.Z;
    }

    public GoalGetToBlock(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public BetterBlockPos GetGoalPos()
    {
        return new BetterBlockPos(X, Y, Z);
    }

    public override bool IsInGoal(int x, int y, int z)
    {
        int xDiff = x - X;
        int yDiff = y - Y;
        int zDiff = z - Z;
        return Math.Abs(xDiff) + Math.Abs(yDiff < 0 ? yDiff + 1 : yDiff) + Math.Abs(zDiff) <= 1;
    }

    public override double Heuristic(int x, int y, int z)
    {
        int xDiff = x - X;
        int yDiff = y - Y;
        int zDiff = z - Z;
        return GoalBlock.Calculate(xDiff, yDiff < 0 ? yDiff + 1 : yDiff, zDiff);
    }

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (obj is not GoalGetToBlock goal)
        {
            return false;
        }
        return X == goal.X && Y == goal.Y && Z == goal.Z;
    }

    public override int GetHashCode()
    {
        return (int)(BetterBlockPos.LongHash(X, Y, Z) * -49639096);
    }

    public override string ToString()
    {
        return $"GoalGetToBlock{{x={X},y={Y},z={Z}}}";
    }
}

