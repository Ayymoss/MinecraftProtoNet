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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/Favoring.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;

namespace MinecraftProtoNet.Baritone.Utils.Pathing;

/// <summary>
/// Favoring system for pathfinding.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/Favoring.java
/// </summary>
public sealed class Favoring
{
    private readonly Dictionary<long, double> _favorings;

    public Favoring(IPlayerContext ctx, IPath? previous, CalculationContext context)
    {
        _favorings = new Dictionary<long, double>();
        _favorings[0] = 1.0D; // Default return value equivalent
        double coeff = context.BacktrackCostFavoringCoefficient;
        if (coeff != 1D && previous != null)
        {
            foreach (var pos in previous.Positions())
            {
                _favorings[BetterBlockPos.LongHash(pos.X, pos.Y, pos.Z)] = coeff;
            }
        }
        foreach (var avoid in Avoidance.Create(ctx))
        {
            avoid.ApplySpherical(_favorings);
        }
    }

    public Favoring(IPath? previous, CalculationContext context)
    {
        _favorings = new Dictionary<long, double>();
        _favorings[0] = 1.0D; // Default return value equivalent
        double coeff = context.BacktrackCostFavoringCoefficient;
        if (coeff != 1D && previous != null)
        {
            foreach (var pos in previous.Positions())
            {
                _favorings[BetterBlockPos.LongHash(pos.X, pos.Y, pos.Z)] = coeff;
            }
        }
    }

    public bool IsEmpty() => _favorings.Count == 0;

    public double Calculate(long hash)
    {
        return _favorings.GetValueOrDefault(hash, 1.0D);
    }
}

