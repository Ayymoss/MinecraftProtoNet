using Microsoft.Extensions.Logging;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Pathfinding;

namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// Context for pathfinding calculations containing world state and settings.
/// Based on Baritone's CalculationContext.java.
/// </summary>
public class CalculationContext(Level level)
{
    /// <summary>
    /// The level/world to pathfind in.
    /// </summary>
    public Level Level { get; } = level;

    /// <summary>
    /// Optional logger for diagnostic output from movements.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Whether sprinting is allowed.
    /// </summary>
    public bool CanSprint { get; set; } = true;

    /// <summary>
    /// Whether block breaking is allowed.
    /// </summary>
    public bool AllowBreak { get; set; } = true;

    /// <summary>
    /// Whether block placing is allowed.
    /// </summary>
    public bool AllowPlace { get; set; } = true;

    /// <summary>
    /// Whether parkour jumps are allowed.
    /// </summary>
    public bool AllowParkour { get; set; } = true;

    /// <summary>
    /// Whether ascending parkour (landing 1 block higher) is allowed.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java
    /// </summary>
    public bool AllowParkourAscend { get; set; } = true;

    /// <summary>
    /// Whether parkour with block placement is allowed.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java
    /// </summary>
    public bool AllowParkourPlace { get; set; } = true;

    /// <summary>
    /// Whether diagonal ascending is allowed.
    /// </summary>
    public bool AllowDiagonalAscend { get; set; } = true;

    /// <summary>
    /// Whether diagonal descending is allowed.
    /// </summary>
    public bool AllowDiagonalDescend { get; set; } = true;

    /// <summary>
    /// Whether downward digging is allowed.
    /// </summary>
    public bool AllowDownward { get; set; } = true;

    /// <summary>
    /// Maximum fall height without water bucket (in blocks).
    /// </summary>
    public int MaxFallHeightNoWater { get; set; } = 3;

    /// <summary>
    /// Maximum fall height with water bucket (in blocks).
    /// </summary>
    public int MaxFallHeightBucket { get; set; } = 20;

    /// <summary>
    /// Whether we have a water bucket for falls.
    /// </summary>
    public bool HasWaterBucket { get; set; } = false;

    /// <summary>
    /// Whether we have throwaway blocks for bridging.
    /// </summary>
    public bool HasThrowaway { get; set; } = true;

    /// <summary>
    /// Whether to assume we can walk on water (frost walker, etc).
    /// </summary>
    public bool AssumeWalkOnWater { get; set; } = false;

    /// <summary>
    /// Frost Walker enchantment level (0 = none, 1-2 = level).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java:115-127
    /// </summary>
    public int FrostWalker { get; set; } = 0;

    /// <summary>
    /// Minimum fall height before considering a fall valid (in blocks).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java
    /// </summary>
    public int MinFallHeight { get; set; } = 3;

    /// <summary>
    /// Penalty for walking on water (one block).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java
    /// </summary>
    public double WalkOnWaterOnePenalty { get; set; } = 0.0;

    /// <summary>
    /// Water walk speed multiplier.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java
    /// </summary>
    public double WaterWalkSpeed { get; set; } = ActionCosts.WalkOneInWaterCost;

    /// <summary>
    /// Whether falling into lava is allowed.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java
    /// </summary>
    public bool AllowFallIntoLava { get; set; } = false;

    /// <summary>
    /// Additional cost penalty for breaking blocks.
    /// </summary>
    public double BreakBlockAdditionalCost { get; set; } = 2.0;

    /// <summary>
    /// Additional cost penalty for placing blocks.
    /// </summary>
    public double PlaceBlockCost { get; set; } = 20.0;

    /// <summary>
    /// Penalty for jumping (encourages flat paths).
    /// </summary>
    public double JumpPenalty { get; set; } = 2.0;

    /// <summary>
    /// Multiplier for the A* heuristic (3.563 for Baritone).
    /// </summary>
    public double CostHeuristic { get; set; } = 3.563;

    /// <summary>
    /// Minimum Y level in the world.
    /// </summary>
    public int MinY { get; set; } = -64;

    /// <summary>
    /// Maximum Y level in the world.
    /// </summary>
    public int MaxY { get; set; } = 320;

    /// <summary>
    /// Checks if the given chunk coordinates are loaded.
    /// </summary>
    public bool IsLoaded(int x, int z)
    {
        var chunkX = x >> 4;
        var chunkZ = z >> 4;
        return Level.HasChunk(chunkX, chunkZ);
    }

    /// <summary>
    /// Gets the block state at the given position.
    /// Returns null if the chunk is not loaded.
    /// </summary>
    public Models.World.Chunk.BlockState? GetBlockState(int x, int y, int z)
    {
        return Level.GetBlockAt(x, y, z);
    }

    /// <summary>
    /// Calculates the cost of placing a block at the given position.
    /// </summary>
    public double CostOfPlacingAt(int x, int y, int z)
    {
        if (!HasThrowaway || !AllowPlace)
        {
            return ActionCosts.CostInf;
        }
        return PlaceBlockCost + ActionCosts.WalkOneBlockCost; // Time to place
    }

    /// <summary>
    /// Calculates the break cost multiplier for the block at the given position.
    /// Returns CostInf if breaking is not allowed.
    /// </summary>
    public double BreakCostMultiplierAt(int x, int y, int z)
    {
        if (!AllowBreak)
        {
            return ActionCosts.CostInf;
        }
        return 1.0 + BreakBlockAdditionalCost;
    }

    /// <summary>
    /// Callback to get the best tool speed against a block.
    /// </summary>
    public Func<Models.World.Chunk.BlockState, float>? GetBestToolSpeed { get; set; }

    /// <summary>
    /// Cost multiplier for backtracking (avoiding previous path positions).
    /// Default 0.5 means backtracking costs 50% more.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java:82
    /// </summary>
    public double BacktrackCostFavoringCoefficient { get; set; } = 0.5;

    /// <summary>
    /// World border checker for validating movements.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/CalculationContext.java:85
    /// </summary>
    public BetterWorldBorder? WorldBorder { get; set; }
}
