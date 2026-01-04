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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/Settings.java
 */

using System.Collections.Immutable;
using System.Reflection;

namespace MinecraftProtoNet.Baritone.Settings;

/// <summary>
/// Baritone's settings. Settings apply to all Baritone instances.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/Settings.java
/// </summary>
public sealed class Settings
{
    // A map of lowercase setting field names to their respective setting
    public readonly IReadOnlyDictionary<string, Setting<object>> ByLowerName;

    // A list of all settings
    public readonly IReadOnlyList<Setting<object>> AllSettings;

    public readonly IReadOnlyDictionary<Setting<object>, Type> SettingTypes;

    // ========== BASIC PERMISSIONS ==========

    /// <summary>
    /// Allow Baritone to break blocks
    /// </summary>
    public readonly Setting<bool> AllowBreak = new(true);

    /// <summary>
    /// Blocks that baritone will be allowed to break even with allowBreak set to false
    /// </summary>
    public readonly Setting<List<object>> AllowBreakAnyway = new(new List<object>());

    /// <summary>
    /// Allow Baritone to sprint
    /// </summary>
    public readonly Setting<bool> AllowSprint = new(true);

    /// <summary>
    /// Allow Baritone to place blocks
    /// </summary>
    public readonly Setting<bool> AllowPlace = new(true);

    /// <summary>
    /// Allow Baritone to place blocks in fluid source blocks
    /// </summary>
    public readonly Setting<bool> AllowPlaceInFluidsSource = new(true);

    /// <summary>
    /// Allow Baritone to place blocks in flowing fluid
    /// </summary>
    public readonly Setting<bool> AllowPlaceInFluidsFlow = new(true);

    /// <summary>
    /// Allow Baritone to move items in your inventory to your hotbar
    /// </summary>
    public readonly Setting<bool> AllowInventory = new(false);

    /// <summary>
    /// Wait this many ticks between InventoryBehavior moving inventory items
    /// </summary>
    public readonly Setting<int> TicksBetweenInventoryMoves = new(1);

    /// <summary>
    /// Come to a halt before doing any inventory moves. Intended for anticheat such as 2b2t
    /// </summary>
    public readonly Setting<bool> InventoryMoveOnlyIfStationary = new(false);

    /// <summary>
    /// Disable baritone's auto-tool at runtime, but still assume that another mod will provide auto tool functionality
    /// Specifically, path calculation will still assume that an auto tool will run at execution time, even though
    /// Baritone itself will not do that.
    /// </summary>
    public readonly Setting<bool> AssumeExternalAutoTool = new(false);

    /// <summary>
    /// Automatically select the best available tool
    /// </summary>
    public readonly Setting<bool> AutoTool = new(true);

    // ========== COSTS ==========

    /// <summary>
    /// It doesn't actually take twenty ticks to place a block, this cost is so high
    /// because we want to generally conserve blocks which might be limited.
    /// Decrease to make Baritone more often consider paths that would require placing blocks
    /// </summary>
    public readonly Setting<double> BlockPlacementPenalty = new(20.0);

    /// <summary>
    /// This is just a tiebreaker to make it less likely to break blocks if it can avoid it.
    /// For example, fire has a break cost of 0, this makes it nonzero, so all else being equal
    /// it will take an otherwise equivalent route that doesn't require it to put out fire.
    /// </summary>
    public readonly Setting<double> BlockBreakAdditionalPenalty = new(2.0);

    /// <summary>
    /// Additional penalty for hitting the space bar (ascend, pillar, or parkour) because it uses hunger
    /// </summary>
    public readonly Setting<double> JumpPenalty = new(2.0);

    /// <summary>
    /// Walking on water uses up hunger really quick, so penalize it
    /// </summary>
    public readonly Setting<double> WalkOnWaterOnePenalty = new(3.0);

    // ========== MOVEMENT RESTRICTIONS ==========

    /// <summary>
    /// Don't allow breaking blocks next to liquids.
    /// Enable if you have mods adding custom fluid physics.
    /// </summary>
    public readonly Setting<bool> StrictLiquidCheck = new(false);

    /// <summary>
    /// Allow Baritone to fall arbitrary distances and place a water bucket beneath it.
    /// Reliability: questionable.
    /// </summary>
    public readonly Setting<bool> AllowWaterBucketFall = new(true);

    /// <summary>
    /// Allow Baritone to assume it can walk on still water just like any other block.
    /// This functionality is assumed to be provided by a separate library that might have imported Baritone.
    /// Note: This will prevent some usage of the frostwalker enchantment, like pillaring up from water.
    /// </summary>
    public readonly Setting<bool> AssumeWalkOnWater = new(false);

    /// <summary>
    /// If you have Fire Resistance and Jesus then I guess you could turn this on lol
    /// </summary>
    public readonly Setting<bool> AssumeWalkOnLava = new(false);

    /// <summary>
    /// Assume step functionality; don't jump on an Ascend.
    /// </summary>
    public readonly Setting<bool> AssumeStep = new(false);

    /// <summary>
    /// Assume safe walk functionality; don't sneak on a backplace traverse.
    /// Warning: if you do something janky like sneak-backplace from an ender chest, if this is true
    /// it won't sneak right click, it'll just right click, which means it'll open the chest instead of placing
    /// against it. That's why this defaults to off.
    /// </summary>
    public readonly Setting<bool> AssumeSafeWalk = new(false);

    /// <summary>
    /// If true, parkour is allowed to make jumps when standing on blocks at the maximum height, so player feet is y=256
    /// Defaults to false because this fails on constantiam. Please let me know if this is ever disabled. Please.
    /// </summary>
    public readonly Setting<bool> AllowJumpAtBuildLimit = new(false);

    /// <summary>
    /// Just here so mods that use the API don't break. Does nothing.
    /// </summary>
    [JavaOnly]
    public readonly Setting<bool> AllowJumpAt256 = new(false);

    /// <summary>
    /// This should be monetized it's so good
    /// Defaults to true, but only actually takes effect if allowParkour is also true
    /// </summary>
    public readonly Setting<bool> AllowParkourAscend = new(true);

    /// <summary>
    /// Allow descending diagonally
    /// Safer than allowParkour yet still slightly unsafe, can make contact with unchecked adjacent blocks, so it's unsafe in the nether.
    /// For a generic "take some risks" mode I'd turn on this one, parkour, and parkour place.
    /// </summary>
    public readonly Setting<bool> AllowDiagonalDescend = new(false);

    /// <summary>
    /// Allow diagonal ascending
    /// Actually pretty safe, much safer than diagonal descend tbh
    /// </summary>
    public readonly Setting<bool> AllowDiagonalAscend = new(false);

    /// <summary>
    /// Allow mining the block directly beneath its feet
    /// Turn this off to force it to make more staircases and less shafts
    /// </summary>
    public readonly Setting<bool> AllowDownward = new(true);

    // ========== BLOCKS AND ITEMS ==========

    /// <summary>
    /// Blocks that Baritone is allowed to place (as throwaway, for sneak bridging, pillaring, etc.)
    /// </summary>
    public readonly Setting<List<object>> AcceptableThrowawayItems = new(new List<object>()); // Will be populated with actual items when integrated

    /// <summary>
    /// Blocks that Baritone will attempt to avoid (Used in avoidance)
    /// </summary>
    public readonly Setting<List<object>> BlocksToAvoid = new(new List<object>());

    /// <summary>
    /// Blocks that Baritone is not allowed to break
    /// </summary>
    public readonly Setting<List<object>> BlocksToDisallowBreaking = new(new List<object>());

    /// <summary>
    /// blocks that baritone shouldn't break, but can if it needs to.
    /// </summary>
    public readonly Setting<List<object>> BlocksToAvoidBreaking = new(new List<object>()); // Will be populated with actual blocks when integrated

    /// <summary>
    /// this multiplies the break speed, if set above 1 it's "encourage breaking" instead
    /// </summary>
    public readonly Setting<double> AvoidBreakingMultiplier = new(0.1);

    // ========== BUILDING ==========

    /// <summary>
    /// A list of blocks to be treated as if they're air.
    /// If a schematic asks for air at a certain position, and that position currently contains a block on this list, it will be treated as correct.
    /// </summary>
    public readonly Setting<List<object>> BuildIgnoreBlocks = new(new List<object>());

    /// <summary>
    /// A list of blocks to be treated as correct.
    /// If a schematic asks for any block on this list at a certain position, it will be treated as correct, regardless of what it currently is.
    /// </summary>
    public readonly Setting<List<object>> BuildSkipBlocks = new(new List<object>());

    /// <summary>
    /// A mapping of blocks to blocks treated as correct in their position
    /// If a schematic asks for a block on this mapping, all blocks on the mapped list will be accepted at that location as well
    /// </summary>
    public readonly Setting<Dictionary<object, List<object>>> BuildValidSubstitutes = new(new Dictionary<object, List<object>>());

    /// <summary>
    /// A mapping of blocks to blocks to be built instead
    /// If a schematic asks for a block on this mapping, Baritone will place the first placeable block in the mapped list
    /// </summary>
    public readonly Setting<Dictionary<object, List<object>>> BuildSubstitutes = new(new Dictionary<object, List<object>>());

    /// <summary>
    /// A list of blocks to become air
    /// If a schematic asks for a block on this list, only air will be accepted at that location (and nothing on buildIgnoreBlocks)
    /// </summary>
    public readonly Setting<List<object>> OkIfAir = new(new List<object>());

    /// <summary>
    /// If this is true, the builder will treat all non-air blocks as correct. It will only place new blocks.
    /// </summary>
    public readonly Setting<bool> BuildIgnoreExisting = new(false);

    /// <summary>
    /// If this is true, the builder will ignore directionality of certain blocks like glazed terracotta.
    /// </summary>
    public readonly Setting<bool> BuildIgnoreDirection = new(false);

    /// <summary>
    /// A list of names of block properties the builder will ignore.
    /// </summary>
    public readonly Setting<List<string>> BuildIgnoreProperties = new(new List<string>());

    /// <summary>
    /// If this setting is true, Baritone will never break a block that is adjacent to an unsupported falling block.
    /// I.E. it will never trigger cascading sand / gravel falls
    /// </summary>
    public readonly Setting<bool> AvoidUpdatingFallingBlocks = new(true);

    /// <summary>
    /// Enables some more advanced vine features. They're honestly just gimmicks and won't ever be needed in real
    /// pathing scenarios. And they can cause Baritone to get trapped indefinitely in a strange scenario.
    /// Almost never turn this on lol
    /// </summary>
    public readonly Setting<bool> AllowVines = new(false);

    /// <summary>
    /// Slab behavior is complicated, disable this for higher path reliability. Leave enabled if you have bottom slabs
    /// everywhere in your base.
    /// </summary>
    public readonly Setting<bool> AllowWalkOnBottomSlab = new(true);

    /// <summary>
    /// You know what it is
    /// But it's very unreliable and falls off when cornering like all the time so.
    /// It also overshoots the landing pretty much always (making contact with the next block over), so be careful
    /// </summary>
    public readonly Setting<bool> AllowParkour = new(false);

    /// <summary>
    /// Actually pretty reliable.
    /// Doesn't make it any more dangerous compared to just normal allowParkour th
    /// </summary>
    public readonly Setting<bool> AllowParkourPlace = new(false);

    /// <summary>
    /// For example, if you have Mining Fatigue or Haste, adjust the costs of breaking blocks accordingly.
    /// </summary>
    public readonly Setting<bool> ConsiderPotionEffects = new(true);

    /// <summary>
    /// Sprint and jump a block early on ascends wherever possible
    /// </summary>
    public readonly Setting<bool> SprintAscends = new(true);

    /// <summary>
    /// If we overshoot a traverse and end up one block beyond the destination, mark it as successful anyway.
    /// This helps with speed exceeding 20m/s
    /// </summary>
    public readonly Setting<bool> OvershootTraverse = new(true);

    /// <summary>
    /// When breaking blocks for a movement, wait until all falling blocks have settled before continuing
    /// </summary>
    public readonly Setting<bool> PauseMiningForFallingBlocks = new(true);

    /// <summary>
    /// How many ticks between right clicks are allowed. Default in game is 4
    /// </summary>
    public readonly Setting<int> RightClickSpeed = new(4);

    /// <summary>
    /// How many degrees to randomize the yaw every tick. Set to 0 to disable
    /// </summary>
    public readonly Setting<double> RandomLooking113 = new(2.0);

    /// <summary>
    /// Block reach distance
    /// </summary>
    public readonly Setting<float> BlockReachDistance = new(4.5f);

    /// <summary>
    /// How many ticks between breaking a block and starting to break the next block. Default in game is 6 ticks.
    /// Values under 1 will be clamped. The delay only applies to non-instant (1-tick) breaks.
    /// </summary>
    public readonly Setting<int> BlockBreakSpeed = new(6);

    /// <summary>
    /// How many degrees to randomize the pitch and yaw every tick. Set to 0 to disable
    /// </summary>
    public readonly Setting<double> RandomLooking = new(0.01);

    // ========== PATHFINDING ==========

    /// <summary>
    /// This is the big A* setting.
    /// As long as your cost heuristic is an *underestimate*, it's guaranteed to find you the best path.
    /// 3.5 is always an underestimate, even if you are sprinting.
    /// If you're walking only (with allowSprint off) 4.6 is safe.
    /// Any value below 3.5 is never worth it. It's just more computation to find the same path, guaranteed.
    /// (specifically, it needs to be strictly slightly less than ActionCosts.WALK_ONE_BLOCK_COST, which is about 3.56)
    /// Setting it at 3.57 or above with sprinting, or to 4.64 or above without sprinting, will result in
    /// faster computation, at the cost of a suboptimal path. Any value above the walk / sprint cost will result
    /// in it going straight at its goal, and not investigating alternatives, because the combined cost / heuristic
    /// metric gets better and better with each block, instead of slightly worse.
    /// Finding the optimal path is worth it, so it's the default.
    /// </summary>
    public readonly Setting<double> CostHeuristic = new(3.563);

    /// <summary>
    /// The maximum number of times it will fetch outside loaded or cached chunks before assuming that
    /// pathing has reached the end of the known area, and should therefore stop.
    /// </summary>
    public readonly Setting<int> PathingMaxChunkBorderFetch = new(50);

    /// <summary>
    /// Set to 1.0 to effectively disable this feature
    /// </summary>
    public readonly Setting<double> BacktrackCostFavoringCoefficient = new(0.5);

    /// <summary>
    /// Toggle the following 4 settings
    /// They have a noticeable performance impact, so they default off
    /// Specifically, building up the avoidance map on the main thread before pathing starts actually takes a noticeable
    /// amount of time, especially when there are a lot of mobs around, and your game jitters for like 200ms while doing so
    /// </summary>
    public readonly Setting<bool> Avoidance = new(false);

    /// <summary>
    /// Set to 1.0 to effectively disable this feature
    /// Set below 1.0 to go out of your way to walk near mob spawners
    /// </summary>
    public readonly Setting<double> MobSpawnerAvoidanceCoefficient = new(2.0);

    /// <summary>
    /// Distance to avoid mob spawners.
    /// </summary>
    public readonly Setting<int> MobSpawnerAvoidanceRadius = new(16);

    /// <summary>
    /// Set to 1.0 to effectively disable this feature
    /// Set below 1.0 to go out of your way to walk near mobs
    /// </summary>
    public readonly Setting<double> MobAvoidanceCoefficient = new(1.5);

    /// <summary>
    /// Distance to avoid mobs.
    /// </summary>
    public readonly Setting<int> MobAvoidanceRadius = new(8);

    /// <summary>
    /// When running a goto towards a container block (chest, ender chest, furnace, etc),
    /// right click and open it once you arrive.
    /// </summary>
    public readonly Setting<bool> RightClickContainerOnArrival = new(true);

    /// <summary>
    /// When running a goto towards a nether portal block, walk all the way into the portal
    /// instead of stopping one block before.
    /// </summary>
    public readonly Setting<bool> EnterPortal = new(true);

    /// <summary>
    /// Don't repropagate cost improvements below 0.01 ticks. They're all just floating point inaccuracies,
    /// and there's no point.
    /// </summary>
    public readonly Setting<bool> MinimumImprovementRepropagation = new(true);

    /// <summary>
    /// After calculating a path (potentially through cached chunks), artificially cut it off to just the part that is
    /// entirely within currently loaded chunks. Improves path safety because cached chunks are heavily simplified.
    /// This is much safer to leave off now, and makes pathing more efficient. More explanation in the issue.
    /// </summary>
    public readonly Setting<bool> CutoffAtLoadBoundary = new(false);

    /// <summary>
    /// If a movement's cost increases by more than this amount between calculation and execution (due to changes
    /// in the environment / world), cancel and recalculate
    /// </summary>
    public readonly Setting<double> MaxCostIncrease = new(10.0);

    /// <summary>
    /// Stop 5 movements before anything that made the path COST_INF.
    /// For example, if lava has spread across the path, don't walk right up to it then recalculate, it might
    /// still be spreading lol
    /// </summary>
    public readonly Setting<int> CostVerificationLookahead = new(5);

    /// <summary>
    /// Static cutoff factor. 0.9 means cut off the last 10% of all paths, regardless of chunk load state
    /// </summary>
    public readonly Setting<double> PathCutoffFactor = new(0.9);

    /// <summary>
    /// Only apply static cutoff for paths of at least this length (in terms of number of movements)
    /// </summary>
    public readonly Setting<int> PathCutoffMinimumLength = new(30);

    /// <summary>
    /// Start planning the next path once the remaining movements tick estimates sum up to less than this value
    /// </summary>
    public readonly Setting<int> PlanningTickLookahead = new(150);

    /// <summary>
    /// Default size of the Long2ObjectOpenHashMap used in pathing
    /// </summary>
    public readonly Setting<int> PathingMapDefaultSize = new(1024);

    /// <summary>
    /// Load factor coefficient for the Long2ObjectOpenHashMap used in pathing
    /// Decrease for faster map operations, but higher memory usage
    /// </summary>
    public readonly Setting<float> PathingMapLoadFactor = new(0.75f);

    /// <summary>
    /// How far are you allowed to fall onto solid ground (without a water bucket)?
    /// 3 won't deal any damage. But if you just want to get down the mountain quickly and you have
    /// Feather Falling IV, you might set it a bit higher, like 4 or 5.
    /// </summary>
    public readonly Setting<int> MaxFallHeightNoWater = new(3);

    /// <summary>
    /// How far are you allowed to fall onto solid ground (with a water bucket)?
    /// It's not that reliable, so I've set it below what would kill an unarmored player (23)
    /// </summary>
    public readonly Setting<int> MaxFallHeightBucket = new(20);

    /// <summary>
    /// Is it okay to sprint through a descend followed by a diagonal?
    /// The player overshoots the landing, but not enough to fall off. And the diagonal ensures that there isn't
    /// lava or anything that's !canWalkInto in that space, so it's technically safe, just a little sketchy.
    /// Note: this is *not* related to the allowDiagonalDescend setting, that is a completely different thing.
    /// </summary>
    public readonly Setting<bool> AllowOvershootDiagonalDescend = new(true);

    /// <summary>
    /// If your goal is a GoalBlock in an unloaded chunk, assume it's far enough away that the Y coord
    /// doesn't matter yet, and replace it with a GoalXZ to the same place before calculating a path.
    /// Once a segment ends within chunk load range of the GoalBlock, it will go back to normal behavior
    /// of considering the Y coord. The reasoning is that if your X and Z are 10,000 blocks away,
    /// your Y coordinate's accuracy doesn't matter at all until you get much much closer.
    /// </summary>
    public readonly Setting<bool> SimplifyUnloadedYCoord = new(true);

    /// <summary>
    /// Whenever a block changes, repack the whole chunk that it's in
    /// </summary>
    public readonly Setting<bool> RepackOnAnyBlockChange = new(true);

    /// <summary>
    /// If a movement takes this many ticks more than its initial cost estimate, cancel it
    /// </summary>
    public readonly Setting<int> MovementTimeoutTicks = new(100);

    /// <summary>
    /// Pathing ends after this amount of time, but only if a path has been found
    /// If no valid path (length above the minimum) has been found, pathing continues up until the failure timeout
    /// </summary>
    public readonly Setting<long> PrimaryTimeoutMs = new(500L);

    /// <summary>
    /// Pathing can never take longer than this, even if that means failing to find any path at all
    /// </summary>
    public readonly Setting<long> FailureTimeoutMs = new(2000L);

    /// <summary>
    /// Planning ahead while executing a segment ends after this amount of time, but only if a path has been found
    /// If no valid path (length above the minimum) has been found, pathing continues up until the failure timeout
    /// </summary>
    public readonly Setting<long> PlanAheadPrimaryTimeoutMs = new(4000L);

    /// <summary>
    /// Planning ahead while executing a segment can never take longer than this, even if that means failing to find any path at all
    /// </summary>
    public readonly Setting<long> PlanAheadFailureTimeoutMs = new(5000L);

    /// <summary>
    /// For debugging, consider nodes much much slower
    /// </summary>
    public readonly Setting<bool> SlowPath = new(false);

    /// <summary>
    /// Milliseconds between each node
    /// </summary>
    public readonly Setting<long> SlowPathTimeDelayMs = new(100L);

    /// <summary>
    /// The alternative timeout number when slowPath is on
    /// </summary>
    public readonly Setting<long> SlowPathTimeoutMs = new(40000L);

    // ========== WAYPOINTS ==========

    /// <summary>
    /// allows baritone to save bed waypoints when interacting with beds
    /// </summary>
    public readonly Setting<bool> DoBedWaypoints = new(true);

    /// <summary>
    /// allows baritone to save death waypoints
    /// </summary>
    public readonly Setting<bool> DoDeathWaypoints = new(true);

    // ========== CACHING ==========

    /// <summary>
    /// The big one. Download all chunks in simplified 2-bit format and save them for better very-long-distance pathing.
    /// </summary>
    public readonly Setting<bool> ChunkCaching = new(true);

    /// <summary>
    /// On save, delete from RAM any cached regions that are more than 1024 blocks away from the player
    /// Temporarily disabled
    /// Temporarily reenabled
    /// </summary>
    public readonly Setting<bool> PruneRegionsFromRam = new(true);

    /// <summary>
    /// The chunk packer queue can never grow to larger than this, if it does, the oldest chunks are discarded
    /// The newest chunks are kept, so that if you're moving in a straight line quickly then stop, your immediate render distance is still included
    /// </summary>
    public readonly Setting<int> ChunkPackerQueueMaxSize = new(2000);

    /// <summary>
    /// Fill in blocks behind you
    /// </summary>
    public readonly Setting<bool> Backfill = new(false);

    // ========== LOGGING AND CHAT ==========

    /// <summary>
    /// Shows popup message in the upper right corner, similarly to when you make an advancement
    /// </summary>
    public readonly Setting<bool> LogAsToast = new(false);

    /// <summary>
    /// Print all the debug messages to chat
    /// </summary>
    public readonly Setting<bool> ChatDebug = new(false);

    /// <summary>
    /// Allow chat based control of Baritone. Most likely should be disabled when Baritone is imported for use in
    /// something else
    /// </summary>
    public readonly Setting<bool> ChatControl = new(true);

    /// <summary>
    /// Some clients like Impact try to force chatControl to off, so here's a second setting to do it anyway
    /// </summary>
    public readonly Setting<bool> ChatControlAnyway = new(false);

    /// <summary>
    /// Debug path completion messages
    /// </summary>
    public readonly Setting<bool> DebugPathCompletion = new(false);

    /// <summary>
    /// Use elytra look behavior for elytra flight
    /// </summary>
    public readonly Setting<bool> ElytraLookBehavior = new(true);

    /// <summary>
    /// Rotate to break blocks
    /// </summary>
    public readonly Setting<bool> RotateToBreakBlocks = new(true);

    /// <summary>
    /// Rotate to place blocks
    /// </summary>
    public readonly Setting<bool> RotateToPlaceBlocks = new(true);

    // ========== RENDERING (excluded for headless but kept for API completeness) ==========

    /// <summary>
    /// Render the path
    /// </summary>
    public readonly Setting<bool> RenderPath = new(true);

    /// <summary>
    /// Render the path as a line instead of a frickin thingy
    /// </summary>
    public readonly Setting<bool> RenderPathAsLine = new(false);

    /// <summary>
    /// Render the goal
    /// </summary>
    public readonly Setting<bool> RenderGoal = new(true);

    /// <summary>
    /// Render the goal as a sick animated thingy instead of just a box
    /// (also controls animation of GoalXZ if renderGoalXZBeacon is enabled)
    /// </summary>
    public readonly Setting<bool> RenderGoalAnimated = new(true);

    /// <summary>
    /// Render selection boxes
    /// </summary>
    public readonly Setting<bool> RenderSelectionBoxes = new(true);

    /// <summary>
    /// Ignore depth when rendering the goal
    /// </summary>
    public readonly Setting<bool> RenderGoalIgnoreDepth = new(true);

    /// <summary>
    /// Renders X/Z type Goals with the vanilla beacon beam effect. Combining this with
    /// renderGoalIgnoreDepth will cause strange render clipping.
    /// </summary>
    public readonly Setting<bool> RenderGoalXzBeacon = new(false);

    /// <summary>
    /// Ignore depth when rendering the selection boxes (to break, to place, to walk into)
    /// </summary>
    public readonly Setting<bool> RenderSelectionBoxesIgnoreDepth = new(true);

    /// <summary>
    /// Ignore depth when rendering the path
    /// </summary>
    public readonly Setting<bool> RenderPathIgnoreDepth = new(true);

    /// <summary>
    /// Line width of the path when rendered, in pixels
    /// </summary>
    public readonly Setting<float> PathRenderLineWidthPixels = new(5.0f);

    /// <summary>
    /// Line width of the goal when rendered, in pixels
    /// </summary>
    public readonly Setting<float> GoalRenderLineWidthPixels = new(3.0f);

    /// <summary>
    /// Start fading out the path at 20 movements ahead, and stop rendering it entirely 30 movements ahead.
    /// Improves FPS.
    /// </summary>
    public readonly Setting<bool> FadePath = new(false);

    /// <summary>
    /// 😎 Render cached chunks as semitransparent. Doesn't work with OptiFine 😭 Rarely randomly crashes, see this issue.
    /// Can be very useful on servers with low render distance. After enabling, you may need to reload the world in order for it to have an effect
    /// (e.g. disconnect and reconnect, enter then exit the nether, die and respawn, etc). This may literally kill your FPS and CPU because
    /// every chunk gets recompiled twice as much as normal, since the cached version comes into range, then the normal one comes from the server for real.
    /// Note that flowing water is cached as AVOID, which is rendered as lava. As you get closer, you may therefore see lava falls being replaced with water falls.
    /// SOLID is rendered as stone in the overworld, netherrack in the nether, and end stone in the end
    /// </summary>
    public readonly Setting<bool> RenderCachedChunks = new(false);

    /// <summary>
    /// 0.0f = not visible, fully transparent (instead of setting this to 0, turn off renderCachedChunks)
    /// 1.0f = fully opaque
    /// </summary>
    public readonly Setting<float> CachedChunksOpacity = new(0.5f);

    // ========== LOOK BEHAVIOR ==========

    /// <summary>
    /// Move without having to force the client-sided rotations
    /// </summary>
    public readonly Setting<bool> FreeLook = new(true);

    /// <summary>
    /// Break and place blocks without having to force the client-sided rotations. Requires freeLook.
    /// </summary>
    public readonly Setting<bool> BlockFreeLook = new(false);

    /// <summary>
    /// Automatically elytra fly without having to force the client-sided rotations.
    /// </summary>
    public readonly Setting<bool> ElytraFreeLook = new(true);

    /// <summary>
    /// Forces the client-sided yaw rotation to an average of the last smoothLookTicks of server-sided rotations.
    /// </summary>
    public readonly Setting<bool> SmoothLook = new(false);

    /// <summary>
    /// Same as smoothLook but for elytra flying.
    /// </summary>
    public readonly Setting<bool> ElytraSmoothLook = new(false);

    /// <summary>
    /// The number of ticks to average across for smoothLook;
    /// </summary>
    public readonly Setting<int> SmoothLookTicks = new(5);

    /// <summary>
    /// When true, the player will remain with its existing look direction as often as possible.
    /// Although, in some cases this can get it stuck, hence this setting to disable that behavior.
    /// </summary>
    public readonly Setting<bool> RemainWithExistingLookDirection = new(true);

    /// <summary>
    /// Will cause some minor behavioral differences to ensure that Baritone works on anticheats.
    /// At the moment this will silently set the player's rotations when using freeLook so you're not sprinting in
    /// directions other than forward, which is picken up by more "advanced" anticheats like AAC, but not NCP.
    /// </summary>
    public readonly Setting<bool> AntiCheatCompatibility = new(true);

    /// <summary>
    /// Exclusively use cached chunks for pathing
    /// Never turn this on
    /// </summary>
    public readonly Setting<bool> PathThroughCachedOnly = new(false);

    /// <summary>
    /// Continue sprinting while in water
    /// </summary>
    public readonly Setting<bool> SprintInWater = new(true);

    /// <summary>
    /// When GetToBlockProcess or MineProcess fails to calculate a path, instead of just giving up, mark the closest instance
    /// of that block as "unreachable" and go towards the next closest. GetToBlock expands this search to the whole "vein"; MineProcess does not.
    /// This is because MineProcess finds individual impossible blocks (like one block in a vein that has gravel on top then lava, so it can't break)
    /// Whereas GetToBlock should blacklist the whole "vein" if it can't get to any of them.
    /// </summary>
    public readonly Setting<bool> BlacklistClosestOnFailure = new(true);

    // ========== COMMANDS ==========

    /// <summary>
    /// Whether or not to allow you to run Baritone commands with the prefix
    /// </summary>
    public readonly Setting<bool> PrefixControl = new(true);

    /// <summary>
    /// The command prefix for chat control
    /// </summary>
    public readonly Setting<string> Prefix = new("#");

    /// <summary>
    /// Use a short Baritone prefix [B] instead of [Baritone] when logging to chat
    /// </summary>
    public readonly Setting<bool> ShortBaritonePrefix = new(false);

    /// <summary>
    /// Use a modern message tag instead of a prefix when logging to chat
    /// </summary>
    public readonly Setting<bool> UseMessageTag = new(false);

    /// <summary>
    /// Echo commands to chat when they are run
    /// </summary>
    public readonly Setting<bool> EchoCommands = new(true);

    /// <summary>
    /// Censor coordinates in goals and block positions
    /// </summary>
    public readonly Setting<bool> CensorCoordinates = new(false);

    /// <summary>
    /// Censor arguments to ran commands, to hide, for example, coordinates to #goal
    /// </summary>
    public readonly Setting<bool> CensorRanCommands = new(false);

    /// <summary>
    /// Print out ALL command exceptions as a stack trace to stdout, even simple syntax errors
    /// </summary>
    public readonly Setting<bool> VerboseCommandExceptions = new(false);

    // ========== ITEMS ==========

    /// <summary>
    /// Stop using tools just before they are going to break.
    /// </summary>
    public readonly Setting<bool> ItemSaver = new(false);

    /// <summary>
    /// Durability to leave on the tool when using itemSaver
    /// </summary>
    public readonly Setting<int> ItemSaverThreshold = new(10);

    /// <summary>
    /// Always prefer silk touch tools over regular tools. This will not sacrifice speed, but it will always prefer silk
    /// touch tools over other tools of the same speed. This includes always choosing ANY silk touch tool over your hand.
    /// </summary>
    public readonly Setting<bool> PreferSilkTouch = new(false);

    /// <summary>
    /// Don't stop walking forward when you need to break blocks in your way
    /// </summary>
    public readonly Setting<bool> WalkWhileBreaking = new(true);

    /// <summary>
    /// Use sword to mine.
    /// </summary>
    public readonly Setting<bool> UseSwordToMine = new(true);

    // ========== PATH EXECUTION ==========

    /// <summary>
    /// When a new segment is calculated that doesn't overlap with the current one, but simply begins where the current segment ends,
    /// splice it on and make a longer combined path. If this setting is off, any planned segment will not be spliced and will instead
    /// be the "next path" in PathingBehavior, and will only start after this one ends. Turning this off hurts planning ahead,
    /// because the next segment will exist even if it's very short.
    /// </summary>
    public readonly Setting<bool> SplicePath = new(true);

    /// <summary>
    /// If we are more than 300 movements into the current path, discard the oldest segments, as they are no longer useful
    /// </summary>
    public readonly Setting<int> MaxPathHistoryLength = new(300);

    /// <summary>
    /// If the current path is too long, cut off this many movements from the beginning.
    /// </summary>
    public readonly Setting<int> PathHistoryCutoffAmount = new(50);

    // ========== MINING ==========

    /// <summary>
    /// Rescan for the goal once every 5 ticks.
    /// Set to 0 to disable.
    /// </summary>
    public readonly Setting<int> MineGoalUpdateInterval = new(5);

    /// <summary>
    /// After finding this many instances of the target block in the cache, it will stop expanding outward the chunk search.
    /// </summary>
    public readonly Setting<int> MaxCachedWorldScanCount = new(10);

    /// <summary>
    /// Mine will not scan for or remember more than this many target locations.
    /// Note that the number of locations retrieved from cache is additionaly
    /// limited by maxCachedWorldScanCount.
    /// </summary>
    public readonly Setting<int> MineMaxOreLocationsCount = new(64);

    /// <summary>
    /// Sets the minimum y level whilst mining - set to 0 to turn off.
    /// if world has negative y values, subtract the min world height to get the value to put here
    /// </summary>
    public readonly Setting<int> MinYLevelWhileMining = new(0);

    /// <summary>
    /// Sets the maximum y level to mine ores at.
    /// </summary>
    public readonly Setting<int> MaxYLevelWhileMining = new(2031);

    /// <summary>
    /// This will only allow baritone to mine exposed ores, can be used to stop ore obfuscators on servers that use them.
    /// </summary>
    public readonly Setting<bool> AllowOnlyExposedOres = new(false);

    /// <summary>
    /// When allowOnlyExposedOres is enabled this is the distance around to search.
    /// It is recommended to keep this value low, as it dramatically increases calculation times.
    /// </summary>
    public readonly Setting<int> AllowOnlyExposedOresDistance = new(1);

    /// <summary>
    /// When GetToBlock or non-legit Mine doesn't know any locations for the desired block, explore randomly instead of giving up.
    /// </summary>
    public readonly Setting<bool> ExploreForBlocks = new(true);

    /// <summary>
    /// While exploring the world, offset the closest unloaded chunk by this much in both axes.
    /// This can result in more efficient loading, if you set this to the render distance.
    /// </summary>
    public readonly Setting<int> WorldExploringChunkOffset = new(0);

    /// <summary>
    /// Take the 10 closest chunks, even if they aren't strictly tied for distance metric from origin.
    /// </summary>
    public readonly Setting<int> ExploreChunkSetMinimumSize = new(10);

    /// <summary>
    /// Attempt to maintain Y coordinate while exploring
    /// -1 to disable
    /// </summary>
    public readonly Setting<int> ExploreMaintainY = new(64);

    /// <summary>
    /// While mining, should it also consider dropped items of the correct type as a pathing destination (as well as ore blocks)?
    /// </summary>
    public readonly Setting<bool> MineScanDroppedItems = new(true);

    /// <summary>
    /// While mining, wait this number of milliseconds after mining an ore to see if it will drop an item
    /// instead of immediately going onto the next one
    /// Thanks Louca
    /// </summary>
    public readonly Setting<long> MineDropLoiterDurationMsThanksLouca = new(250L);

    /// <summary>
    /// Disallow MineBehavior from using X-Ray to see where the ores are. Turn this option on to force it to mine "legit"
    /// where it will only mine an ore once it can actually see it, so it won't do or know anything that a normal player
    /// couldn't. If you don't want it to look like you're X-Raying, turn this on
    /// This will always explore, regardless of exploreForBlocks
    /// </summary>
    public readonly Setting<bool> LegitMine = new(false);

    /// <summary>
    /// What Y level to go to for legit strip mining
    /// </summary>
    public readonly Setting<int> LegitMineYLevel = new(-59);

    /// <summary>
    /// Magically see ores that are separated diagonally from existing ores. Basically like mining around the ores that it finds
    /// in case there's one there touching it diagonally, except it checks it un-legit-ly without having the mine blocks to see it.
    /// You can decide whether this looks plausible or not.
    /// This is disabled because it results in some weird behavior. For example, it can """see""" the top block of a vein of iron_ore
    /// through a lava lake. This isn't an issue normally since it won't consider anything touching lava, so it just ignores it.
    /// However, this setting expands that and allows it to see the entire vein so it'll mine under the lava lake to get the iron that
    /// it can reach without mining blocks adjacent to lava. This really defeats the purpose of legitMine since a player could never
    /// do that lol, so thats one reason why its disabled
    /// </summary>
    public readonly Setting<bool> LegitMineIncludeDiagonals = new(false);

    /// <summary>
    /// When mining block of a certain type, try to mine two at once instead of one.
    /// If the block above is also a goal block, set GoalBlock instead of GoalTwoBlocks
    /// If the block below is also a goal block, set GoalBlock to the position one down instead of GoalTwoBlocks
    /// </summary>
    public readonly Setting<bool> ForceInternalMining = new(true);

    /// <summary>
    /// Modification to the previous setting, only has effect if forceInternalMining is true
    /// If true, only apply the previous setting if the block adjacent to the goal isn't air.
    /// </summary>
    public readonly Setting<bool> InternalMiningAirException = new(true);

    // ========== FARMING ==========

    /// <summary>
    /// Replant normal Crops while farming and leave cactus and sugarcane to regrow
    /// </summary>
    public readonly Setting<bool> ReplantCrops = new(true);

    /// <summary>
    /// Replant nether wart while farming. This setting only has an effect when replantCrops is also enabled
    /// </summary>
    public readonly Setting<bool> ReplantNetherWart = new(false);

    /// <summary>
    /// Farming will scan for at most this many blocks.
    /// </summary>
    public readonly Setting<int> FarmMaxScanSize = new(256);

    /// <summary>
    /// When the cache scan gives less blocks than the maximum threshold (but still above zero), scan the main world too.
    /// Only if you have a beefy CPU and automatically mine blocks that are in cache
    /// </summary>
    public readonly Setting<bool> ExtendCacheOnThreshold = new(false);

    // ========== BUILDING ==========

    /// <summary>
    /// Don't consider the next layer in builder until the current one is done
    /// </summary>
    public readonly Setting<bool> BuildInLayers = new(false);

    /// <summary>
    /// false = build from bottom to top
    /// true = build from top to bottom
    /// </summary>
    public readonly Setting<bool> LayerOrder = new(false);

    /// <summary>
    /// How high should the individual layers be?
    /// </summary>
    public readonly Setting<int> LayerHeight = new(1);

    /// <summary>
    /// Start building the schematic at a specific layer.
    /// Can help on larger builds when schematic wants to break things its already built
    /// </summary>
    public readonly Setting<int> StartAtLayer = new(0);

    /// <summary>
    /// If a layer is unable to be constructed, just skip it.
    /// </summary>
    public readonly Setting<bool> SkipFailedLayers = new(false);

    /// <summary>
    /// Only build the selected part of schematics
    /// </summary>
    public readonly Setting<bool> BuildOnlySelection = new(false);

    /// <summary>
    /// How far to move before repeating the build. 0 to disable repeating on a certain axis, 0,0,0 to disable entirely
    /// </summary>
    public readonly Setting<(int X, int Y, int Z)> BuildRepeat = new((0, 0, 0));

    /// <summary>
    /// How many times to buildrepeat. -1 for infinite.
    /// </summary>
    public readonly Setting<int> BuildRepeatCount = new(-1);

    /// <summary>
    /// Don't notify schematics that they are moved.
    /// e.g. replacing will replace the same spots for every repetition
    /// Mainly for backward compatibility.
    /// </summary>
    public readonly Setting<bool> BuildRepeatSneaky = new(true);

    /// <summary>
    /// Allow standing above a block while mining it, in BuilderProcess
    /// Experimental
    /// </summary>
    public readonly Setting<bool> BreakFromAbove = new(false);

    /// <summary>
    /// As well as breaking from above, set a goal to up and to the side of all blocks to break.
    /// Never turn this on without also turning on breakFromAbove.
    /// </summary>
    public readonly Setting<bool> GoalBreakFromAbove = new(false);

    /// <summary>
    /// Build in map art mode, which makes baritone only care about the top block in each column
    /// </summary>
    public readonly Setting<bool> MapArtMode = new(false);

    /// <summary>
    /// Override builder's behavior to not attempt to correct blocks that are currently water
    /// </summary>
    public readonly Setting<bool> OkIfWater = new(false);

    /// <summary>
    /// The set of incorrect blocks can never grow beyond this size
    /// </summary>
    public readonly Setting<int> IncorrectSize = new(100);

    /// <summary>
    /// Multiply the cost of breaking a block that's correct in the builder's schematic by this coefficient
    /// </summary>
    public readonly Setting<double> BreakCorrectBlockPenaltyMultiplier = new(10.0);

    /// <summary>
    /// Multiply the cost of placing a block that's incorrect in the builder's schematic by this coefficient
    /// </summary>
    public readonly Setting<double> PlaceIncorrectBlockPenaltyMultiplier = new(2.0);

    /// <summary>
    /// When this setting is true, build a schematic with the highest X coordinate being the origin, instead of the lowest
    /// </summary>
    public readonly Setting<bool> SchematicOrientationX = new(false);

    /// <summary>
    /// When this setting is true, build a schematic with the highest Y coordinate being the origin, instead of the lowest
    /// </summary>
    public readonly Setting<bool> SchematicOrientationY = new(false);

    /// <summary>
    /// When this setting is true, build a schematic with the highest Z coordinate being the origin, instead of the lowest
    /// </summary>
    public readonly Setting<bool> SchematicOrientationZ = new(false);

    /// <summary>
    /// Rotates the schematic before building it.
    /// Possible values are NONE, CLOCKWISE_90, CLOCKWISE_180, COUNTERCLOCKWISE_90
    /// </summary>
    public readonly Setting<string> BuildSchematicRotation = new("NONE"); // Will be enum when integrated

    /// <summary>
    /// Mirrors the schematic before building it.
    /// Possible values are NONE, FRONT_BACK, LEFT_RIGHT
    /// </summary>
    public readonly Setting<string> BuildSchematicMirror = new("NONE"); // Will be enum when integrated

    /// <summary>
    /// The fallback used by the build command when no extension is specified. This may be useful if schematics of a
    /// particular format are used often, and the user does not wish to have to specify the extension with every usage.
    /// </summary>
    public readonly Setting<string> SchematicFallbackExtension = new("schematic");

    /// <summary>
    /// Distance to scan every tick for updates. Expanding this beyond player reach distance (i.e. setting it to 6 or above)
    /// is only necessary in very large schematics where rescanning the whole thing is costly.
    /// </summary>
    public readonly Setting<int> BuilderTickScanRadius = new(5);

    /// <summary>
    /// Trim incorrect positions too far away, helps performance but hurts reliability in very large schematics
    /// </summary>
    public readonly Setting<bool> DistanceTrim = new(true);

    /// <summary>
    /// Cancel the current path if the goal has changed, and the path originally ended in the goal but doesn't anymore.
    /// Currently only runs when either MineBehavior or FollowBehavior is active.
    /// For example, if Baritone is doing "mine iron_ore", the instant it breaks the ore (and it becomes air), that location
    /// is no longer a goal. This means that if this setting is true, it will stop there. If this setting were off, it would
    /// continue with its path, and walk into that location. The tradeoff is if this setting is true, it mines ores much faster
    /// since it doesn't waste any time getting into locations that no longer contain ores, but on the other hand, it misses
    /// some drops, and continues on without ever picking them up.
    /// Also on cosmic prisons this should be set to true since you don't actually mine the ore it just gets replaced with stone.
    /// </summary>
    public readonly Setting<bool> CancelOnGoalInvalidation = new(true);

    /// <summary>
    /// The "axis" command (aka GoalAxis) will go to a axis, or diagonal axis, at this Y level.
    /// </summary>
    public readonly Setting<int> AxisHeight = new(120);

    /// <summary>
    /// Disconnect from the server upon arriving at your goal
    /// </summary>
    public readonly Setting<bool> DisconnectOnArrival = new(false);

    // ========== FOLLOW ==========

    /// <summary>
    /// The actual GoalNear is set this distance away from the entity you're following
    /// For example, set followOffsetDistance to 5 and followRadius to 0 to always stay precisely 5 blocks north of your follow target.
    /// </summary>
    public readonly Setting<double> FollowOffsetDistance = new(0.0);

    /// <summary>
    /// The actual GoalNear is set in this direction from the entity you're following. This value is in degrees.
    /// </summary>
    public readonly Setting<float> FollowOffsetDirection = new(0.0f);

    /// <summary>
    /// The radius (for the GoalNear) of how close to your target position you actually have to be
    /// </summary>
    public readonly Setting<int> FollowRadius = new(3);

    /// <summary>
    /// The maximum distance to the entity you're following
    /// </summary>
    public readonly Setting<int> FollowTargetMaxDistance = new(0);

    // ========== EXPLORATION ==========

    /// <summary>
    /// Turn this on if your exploration filter is enormous, you don't want it to check if it's done,
    /// and you are just fine with it just hanging on completion
    /// </summary>
    public readonly Setting<bool> DisableCompletionCheck = new(false);

    /// <summary>
    /// Cached chunks (regardless of if they're in RAM or saved to disk) expire and are deleted after this number of seconds
    /// -1 to disable
    /// I would highly suggest leaving this setting disabled (-1).
    /// The only valid reason I can think of enable this setting is if you are extremely low on disk space and you play on multiplayer,
    /// and can't take (average) 300kb saved for every 512x512 area. (note that more complicated terrain is less compressible and will take more space)
    /// However, simply discarding old chunks because they are old is inadvisable. Baritone is extremely good at correcting
    /// itself and its paths as it learns new information, as new chunks load. There is no scenario in which having an
    /// incorrect cache can cause Baritone to get stuck, take damage, or perform any action it wouldn't otherwise, everything
    /// is rechecked once the real chunk is in range.
    /// Having a robust cache greatly improves long distance pathfinding, as it's able to go around large scale obstacles
    /// before they're in render distance. In fact, when the chunkCaching setting is disabled and Baritone starts anew
    /// every time, or when you enter a completely new and very complicated area, it backtracks far more often because it
    /// has to build up that cache from scratch. But after it's gone through an area just once, the next time will have zero
    /// backtracking, since the entire area is now known and cached.
    /// </summary>
    public readonly Setting<long> CachedChunksExpirySeconds = new(-1L);

    // ========== ELYTRA ==========

    /// <summary>
    /// The number of ticks of elytra movement to simulate while firework boost is not active. Higher values are
    /// computationally more expensive.
    /// </summary>
    public readonly Setting<int> ElytraSimulationTicks = new(20);

    /// <summary>
    /// The maximum allowed deviation in pitch from a direct line-of-sight to the flight target. Higher values are
    /// computationally more expensive.
    /// </summary>
    public readonly Setting<int> ElytraPitchRange = new(25);

    /// <summary>
    /// The minimum speed that the player can drop to (in blocks/tick) before a firework is automatically deployed.
    /// </summary>
    public readonly Setting<double> ElytraFireworkSpeed = new(1.2);

    /// <summary>
    /// The delay after the player's position is set-back by the server that a firework may be automatically deployed.
    /// Value is in ticks.
    /// </summary>
    public readonly Setting<int> ElytraFireworkSetbackUseDelay = new(15);

    /// <summary>
    /// The minimum padding value that is added to the player's hitbox when considering which point to fly to on the
    /// path. High values can result in points not being considered which are otherwise safe to fly to. Low values can
    /// result in flight paths which are extremely tight, and there's the possibility of crashing due to getting too low
    /// to the ground.
    /// </summary>
    public readonly Setting<double> ElytraMinimumAvoidance = new(0.2);

    /// <summary>
    /// If enabled, avoids using fireworks when descending along the flight path.
    /// </summary>
    public readonly Setting<bool> ElytraConserveFireworks = new(false);

    /// <summary>
    /// Renders the raytraces that are performed by the elytra fly calculation.
    /// </summary>
    public readonly Setting<bool> ElytraRenderRaytraces = new(false);

    /// <summary>
    /// Renders the raytraces that are used in the hitbox part of the elytra fly calculation.
    /// Requires elytraRenderRaytraces.
    /// </summary>
    public readonly Setting<bool> ElytraRenderHitboxRaytraces = new(false);

    /// <summary>
    /// Renders the best elytra flight path that was simulated each tick.
    /// </summary>
    public readonly Setting<bool> ElytraRenderSimulation = new(true);

    /// <summary>
    /// Automatically path to and jump off of ledges to initiate elytra flight when grounded.
    /// </summary>
    public readonly Setting<bool> ElytraAutoJump = new(false);

    /// <summary>
    /// The seed used to generate chunks for long distance elytra path-finding in the nether.
    /// Defaults to 2b2t's nether seed.
    /// </summary>
    public readonly Setting<long> ElytraNetherSeed = new(146008555100680L);

    /// <summary>
    /// Whether nether-pathfinder should generate terrain based on elytraNetherSeed.
    /// If false all chunks that haven't been loaded are assumed to be air.
    /// </summary>
    public readonly Setting<bool> ElytraPredictTerrain = new(false);

    /// <summary>
    /// Automatically swap the current elytra with a new one when the durability gets too low
    /// </summary>
    public readonly Setting<bool> ElytraAutoSwap = new(true);

    /// <summary>
    /// The minimum durability an elytra can have before being swapped
    /// </summary>
    public readonly Setting<int> ElytraMinimumDurability = new(5);

    /// <summary>
    /// The minimum fireworks before landing early for safety
    /// </summary>
    public readonly Setting<int> ElytraMinFireworksBeforeLanding = new(5);

    /// <summary>
    /// Automatically land when elytra is almost out of durability, or almost out of fireworks
    /// </summary>
    public readonly Setting<bool> ElytraAllowEmergencyLand = new(true);

    /// <summary>
    /// Time between culling far away chunks from the nether pathfinder chunk cache
    /// </summary>
    public readonly Setting<long> ElytraTimeBetweenCacheCullSecs = new(180L); // 3 minutes

    /// <summary>
    /// Maximum distance chunks can be before being culled from the nether pathfinder chunk cache
    /// </summary>
    public readonly Setting<int> ElytraCacheCullDistance = new(5000);

    /// <summary>
    /// Should elytra consider nether brick a valid landing block
    /// </summary>
    public readonly Setting<bool> ElytraAllowLandOnNetherFortress = new(false);

    /// <summary>
    /// Has the user read and understood the elytra terms and conditions
    /// </summary>
    public readonly Setting<bool> ElytraTermsAccepted = new(false);

    /// <summary>
    /// Verbose chat logging in elytra mode
    /// </summary>
    public readonly Setting<bool> ElytraChatSpam = new(false);

    // ========== RENDERING COLORS (excluded for headless but kept for API completeness) ==========

    /// <summary>
    /// The size of the box that is rendered when the current goal is a GoalYLevel
    /// </summary>
    public readonly Setting<double> YLevelBoxSize = new(15.0);

    // Note: Color settings are excluded for headless client but would be here for API completeness

    // ========== SELECTIONS ==========

    /// <summary>
    /// Render selections
    /// </summary>
    public readonly Setting<bool> RenderSelection = new(true);

    /// <summary>
    /// Ignore depth when rendering selections
    /// </summary>
    public readonly Setting<bool> RenderSelectionIgnoreDepth = new(true);

    /// <summary>
    /// Render selection corners
    /// </summary>
    public readonly Setting<bool> RenderSelectionCorners = new(true);

    // ========== NOTIFICATIONS ==========

    /// <summary>
    /// Desktop notifications
    /// </summary>
    public readonly Setting<bool> DesktopNotifications = new(false);

    /// <summary>
    /// Desktop notification on path complete
    /// </summary>
    public readonly Setting<bool> NotificationOnPathComplete = new(true);

    /// <summary>
    /// Desktop notification on farm fail
    /// </summary>
    public readonly Setting<bool> NotificationOnFarmFail = new(true);

    /// <summary>
    /// Desktop notification on build finished
    /// </summary>
    public readonly Setting<bool> NotificationOnBuildFinished = new(true);

    /// <summary>
    /// Desktop notification on explore finished
    /// </summary>
    public readonly Setting<bool> NotificationOnExploreFinished = new(true);

    /// <summary>
    /// Desktop notification on mine fail
    /// </summary>
    public readonly Setting<bool> NotificationOnMineFail = new(true);

    // Constructor that initializes the settings registry
    public Settings()
    {
        var fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        var tmpByName = new Dictionary<string, Setting<object>>(StringComparer.OrdinalIgnoreCase);
        var tmpAll = new List<Setting<object>>();
        var tmpSettingTypes = new Dictionary<Setting<object>, Type>();

        foreach (var field in fields)
        {
            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Setting<>))
            {
                var setting = (Setting<object>)field.GetValue(this)!;
                var name = field.Name;
                setting.SetName(name);
                
                var isJavaOnly = field.GetCustomAttribute<JavaOnlyAttribute>() != null;
                setting.SetJavaOnly(isJavaOnly);

                var lowerName = name.ToLowerInvariant();
                if (tmpByName.ContainsKey(lowerName))
                {
                    throw new InvalidOperationException($"Duplicate setting name: {name}");
                }

                tmpByName[lowerName] = setting;
                tmpAll.Add(setting);

                var genericArgs = field.FieldType.GetGenericArguments();
                tmpSettingTypes[setting] = genericArgs[0];
            }
        }

        ByLowerName = tmpByName.ToImmutableDictionary();
        AllSettings = tmpAll.ToImmutableList();
        SettingTypes = tmpSettingTypes.ToImmutableDictionary();
    }

    /// <summary>
    /// Gets all settings of a specific type.
    /// </summary>
    public List<Setting<T>> GetAllValuesByType<T>()
    {
        var result = new List<Setting<T>>();
        var fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var field in fields)
        {
            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Setting<>))
            {
                var genericArgs = field.FieldType.GetGenericArguments();
                if (genericArgs.Length > 0 && genericArgs[0] == typeof(T))
                {
                    var setting = (Setting<T>)field.GetValue(this)!;
                    result.Add(setting);
                }
            }
        }
        return result;
    }
}

/// <summary>
/// Marks a Setting field as being Java-only (not user-configurable).
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
internal class JavaOnlyAttribute : Attribute
{
}

