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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalStrictDirection.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Physics;

namespace MinecraftProtoNet.Baritone.Pathfinding.Goals;

/// <summary>
/// Dig a tunnel in a certain direction, but if you have to deviate from the path, go back to where you started.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/goals/GoalStrictDirection.java
/// </summary>
public class GoalStrictDirection : Goal
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;
    public readonly int Dx;
    public readonly int Dz;

    public GoalStrictDirection(BetterBlockPos origin, BlockFace direction)
    {
        X = origin.X;
        Y = origin.Y;
        Z = origin.Z;
        var normal = Direction.GetNormal(direction);
        Dx = normal.X;
        Dz = normal.Z;
        if (Dx == 0 && Dz == 0)
        {
            throw new ArgumentException($"Invalid direction: {direction}");
        }
    }

    public GoalStrictDirection(int x, int y, int z, int dx, int dz)
    {
        X = x;
        Y = y;
        Z = z;
        Dx = dx;
        Dz = dz;
        if (Dx == 0 && Dz == 0)
        {
            throw new ArgumentException("dx and dz cannot both be 0");
        }
    }

    public override bool IsInGoal(int x, int y, int z)
    {
        return false;
    }

    public override double Heuristic(int x, int y, int z)
    {
        int distanceFromStartInDesiredDirection = (x - X) * Dx + (z - Z) * Dz;

        int distanceFromStartInIncorrectDirection = Math.Abs((x - X) * Dz) + Math.Abs((z - Z) * Dx);

        int verticalDistanceFromStart = Math.Abs(y - Y);

        // we want heuristic to decrease as desiredDirection increases
        double heuristic = -distanceFromStartInDesiredDirection * 100;

        heuristic += distanceFromStartInIncorrectDirection * 1000;
        heuristic += verticalDistanceFromStart * 1000;
        return heuristic;
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
        if (obj is not GoalStrictDirection goal)
        {
            return false;
        }
        return X == goal.X && Y == goal.Y && Z == goal.Z && Dx == goal.Dx && Dz == goal.Dz;
    }

    public override int GetHashCode()
    {
        int hash = (int)BetterBlockPos.LongHash(X, Y, Z);
        hash = hash * 630627507 + Dx;
        hash = hash * -283028380 + Dz;
        return hash;
    }

    public override string ToString()
    {
        return $"GoalStrictDirection{{x={X}, y={Y}, z={Z}, dx={Dx}, dz={Dz}}}";
    }
}

