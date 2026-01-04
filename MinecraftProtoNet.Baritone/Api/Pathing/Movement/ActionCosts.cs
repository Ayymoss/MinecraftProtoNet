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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/movement/ActionCosts.java
 */

namespace MinecraftProtoNet.Baritone.Api.Pathing.Movement;

/// <summary>
/// Action costs constants. These costs are measured roughly in ticks.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/pathing/movement/ActionCosts.java
/// </summary>
public static class ActionCosts
{
    /// <summary>
    /// Cost to walk one block.
    /// </summary>
    public const double WalkOneBlockCost = 20.0 / 4.317; // 4.633

    /// <summary>
    /// Cost to walk one block in water.
    /// </summary>
    public const double WalkOneInWaterCost = 20.0 / 2.2; // 9.091

    /// <summary>
    /// Cost to walk one block over soul sand.
    /// </summary>
    public const double WalkOneOverSoulSandCost = WalkOneBlockCost * 2; // 0.4 in BlockSoulSand but effectively about half

    /// <summary>
    /// Cost to go up one block on a ladder.
    /// </summary>
    public const double LadderUpOneCost = 20.0 / 2.35; // 8.511

    /// <summary>
    /// Cost to go down one block on a ladder.
    /// </summary>
    public const double LadderDownOneCost = 20.0 / 3.0; // 6.667

    /// <summary>
    /// Cost to sneak one block.
    /// </summary>
    public const double SneakOneBlockCost = 20.0 / 1.3; // 15.385

    /// <summary>
    /// Cost to sprint one block.
    /// </summary>
    public const double SprintOneBlockCost = 20.0 / 5.612; // 3.564

    /// <summary>
    /// Sprint multiplier.
    /// </summary>
    public const double SprintMultiplier = SprintOneBlockCost / WalkOneBlockCost; // 0.769

    /// <summary>
    /// To walk off an edge you need to walk 0.5 to the edge then 0.3 to start falling off.
    /// </summary>
    public const double WalkOffBlockCost = WalkOneBlockCost * 0.8; // 3.706

    /// <summary>
    /// To walk the rest of the way to be centered on the new block.
    /// </summary>
    public const double CenterAfterFallCost = WalkOneBlockCost - WalkOffBlockCost; // 0.927

    /// <summary>
    /// Don't make this Double.MaxValue because it's added to other things, maybe other COST_INFs,
    /// and that would make it overflow to negative.
    /// </summary>
    public const double CostInf = 1000000;

    /// <summary>
    /// Cost to fall N blocks.
    /// </summary>
    public static readonly double[] FallNBlocksCost = GenerateFallNBlocksCost();

    /// <summary>
    /// Cost to fall 1.25 blocks.
    /// </summary>
    public static readonly double Fall1_25BlocksCost = DistanceToTicks(1.25);

    /// <summary>
    /// Cost to fall 0.25 blocks.
    /// </summary>
    public static readonly double Fall0_25BlocksCost = DistanceToTicks(0.25);

    /// <summary>
    /// When you hit space, you get enough upward velocity to go 1.25 blocks.
    /// Then, you fall the remaining 0.25 to get on the surface, on block higher.
    /// Since parabolas are symmetric, the amount of time it takes to ascend up from 1 to 1.25
    /// will be the same amount of time that it takes to fall back down from 1.25 to 1.
    /// And the same applies to the overall shape, if it takes X ticks to fall back down 1.25 blocks,
    /// it will take X ticks to reach the peak of your 1.25 block leap.
    /// Therefore, the part of your jump from y=0 to y=1.25 takes distanceToTicks(1.25) ticks,
    /// and the sub-part from y=1 to y=1.25 takes distanceToTicks(0.25) ticks.
    /// Therefore, the other sub-part, from y=0 to y-1, takes distanceToTicks(1.25)-distanceToTicks(0.25) ticks.
    /// That's why JUMP_ONE_BLOCK_COST = FALL_1_25_BLOCKS_COST - FALL_0_25_BLOCKS_COST
    /// </summary>
    public static readonly double JumpOneBlockCost = Fall1_25BlocksCost - Fall0_25BlocksCost;

    private static double[] GenerateFallNBlocksCost()
    {
        double[] costs = new double[4097];
        for (int i = 0; i < 4097; i++)
        {
            costs[i] = DistanceToTicks(i);
        }
        return costs;
    }

    private static double Velocity(int ticks)
    {
        return (Math.Pow(0.98, ticks) - 1) * -3.92;
    }

    private static double DistanceToTicks(double distance)
    {
        if (distance == 0)
        {
            return 0; // Avoid 0/0 NaN
        }
        double tmpDistance = distance;
        int tickCount = 0;
        while (true)
        {
            double fallDistance = Velocity(tickCount);
            if (tmpDistance <= fallDistance)
            {
                return tickCount + tmpDistance / fallDistance;
            }
            tmpDistance -= fallDistance;
            tickCount++;
        }
    }
}

