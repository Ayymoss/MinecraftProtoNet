namespace MinecraftProtoNet.Pathfinding;

/// <summary>
/// Movement cost constants measured in game ticks.
/// Based on Baritone's ActionCosts.java.
/// These values represent the time cost of various movements.
/// </summary>
public static class ActionCosts
{
    // ===== Basic Movement Costs =====
    // Costs are measured in ticks (20 ticks = 1 second)
    
    public const double WalkOneBlockCost = 20.0 / 4.317; // ~4.633 ticks
    public const double WalkOneInWaterCost = 20.0 / 2.2; // ~9.091 ticks
    public const double WalkOneOverSoulSandCost = WalkOneBlockCost * 2; // ~9.266 ticks
    public const double LadderUpOneCost = 20.0 / 2.35; // ~8.511 ticks
    public const double LadderDownOneCost = 20.0 / 3.0; // ~6.667 ticks
    public const double SneakOneBlockCost = 20.0 / 1.3; // ~15.385 ticks
    public const double SprintOneBlockCost = 20.0 / 5.612; // ~3.564 ticks
    public const double SprintMultiplier = SprintOneBlockCost / WalkOneBlockCost; // ~0.769

    // ===== Edge Traversal Costs =====

    public const double WalkOffBlockCost = WalkOneBlockCost * 0.8; // ~3.706 ticks
    public const double CenterAfterFallCost = WalkOneBlockCost - WalkOffBlockCost; // ~0.927 ticks

    // ===== Fall Costs =====

    public static readonly double[] FallNBlocksCost = GenerateFallNBlocksCost();

    public static readonly double Fall125BlocksCost = DistanceToTicks(1.25);
    public static readonly double Fall025BlocksCost = DistanceToTicks(0.25);
    public static readonly double JumpOneBlockCost = Fall125BlocksCost - Fall025BlocksCost;

    // ===== Infinity =====

    public const double CostInf = 1000000;

    // ===== Physics Formulas =====

    private static double[] GenerateFallNBlocksCost()
    {
        var costs = new double[4097];
        for (var i = 0; i < 4097; i++)
        {
            costs[i] = DistanceToTicks(i);
        }
        return costs;
    }

    public static double Velocity(int ticks)
    {
        return (Math.Pow(0.98, ticks) - 1) * -3.92;
    }

    public static double DistanceToTicks(double distance)
    {
        if (distance == 0) return 0;

        var remainingDistance = distance;
        var tickCount = 0;

        while (true)
        {
            var fallDistance = Velocity(tickCount);
            if (remainingDistance <= fallDistance)
            {
                return tickCount + remainingDistance / fallDistance;
            }
            remainingDistance -= fallDistance;
            tickCount++;
        }
    }

    public static double GetFallCost(int blocks)
    {
        if (blocks < 0 || blocks >= FallNBlocksCost.Length)
        {
            return CostInf;
        }
        return FallNBlocksCost[blocks];
    }

    /// <summary>
    /// Calculates the time in ticks to break a block.
    /// Parity with vanilla Block.getDestroyProgress / Baritone logic.
    /// </summary>
    /// <param name="destroySpeed">The tool's destruction speed against the block (e.g. 8.0 for Diamond Pick vs Stone).</param>
    /// <param name="hardness">The block's hardness value.</param>
    /// <param name="canHarvest">Whether the tool can harvest the block (drops items). If false, digging is slower (1/100 vs 1/30).</param>
    /// <returns>Ticks to break (capped at CostInf if unbreakable).</returns>
    public static double CalculateMiningDuration(float destroySpeed, float hardness, bool canHarvest)
    {
        if (hardness == -1.0f) return CostInf; // Unbreakable
        if (hardness == 0.0f) return 0; // Instant

        float damage = destroySpeed / hardness;
        
        if (canHarvest)
        {
            damage /= 30.0f;
        }
        else
        {
            damage /= 100.0f;
        }

        // Vanilla Instant Break check
        if (damage > 1.0f) return 0;

        // Ticks = ceil(1 / damage)
        return Math.Ceiling(1.0f / damage);
    }
}
