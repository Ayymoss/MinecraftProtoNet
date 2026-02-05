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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:449-472
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Core.Models.World.Chunk;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// Custom calculation context which makes the player fall into lava.
/// Used by ElytraProcess to find a spot to jump off from.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:449-472
/// </summary>
public class WalkOffCalculationContext : CalculationContext
{
    public WalkOffCalculationContext(IBaritone baritone) : base(baritone, true)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:451-455
        AllowFallIntoLava = true;
        MinFallHeight = 8;
        MaxFallHeightNoWater = 10000;
    }

    public override double CostOfPlacingAt(int x, int y, int z, BlockState current)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:459-461
        // Don't allow placing blocks - we want to fall
        return ActionCosts.CostInf;
    }

    public override double BreakCostMultiplierAt(int x, int y, int z, BlockState current)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:463-466
        // Don't allow breaking blocks - we want to fall
        return ActionCosts.CostInf;
    }

    public override double PlaceBucketCost()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:468-471
        // Don't allow placing water bucket - we want to fall
        return ActionCosts.CostInf;
    }
}

