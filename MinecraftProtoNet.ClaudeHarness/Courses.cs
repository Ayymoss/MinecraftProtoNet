namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>
/// Declarative course definitions the harness can build in-world (<c>--build &lt;name&gt;</c>) before running the
/// matching scenario. Each course is a plain list of server commands, sent in order as the (op) bot.
/// Bedrock is the structural block so that <c>--clear-course</c> (which only removes dirt) can never damage a
/// course, and so the bot cannot mine its way through a shortcut.
/// </summary>
public static class Courses
{
    /// <summary>
    /// "course1" — a mixed-discipline course starting at -863 73 -38 and finishing at -883 87 -27.
    ///
    /// Layout (bot walks south/+Z, climbs, then doubles back west):
    ///   1. 5x5 start platform (surface y72, bot spawns on it at y73)
    ///   2. three ascending steps  (y73 -> y75)        exercises MovementAscend
    ///   3. two 3-block parkour gaps (z -33 -> -30 -> -27) exercises MovementParkour
    ///   4. short walkway, then a 12-rung ladder up a bedrock tower (y76 -> y87) exercises ladder climbing
    ///   5. high walkway west at y87 ending over open air
    ///   6. a ~4 block drop off the end into a 3x3 water pool (surface y83) exercises fall-into-water
    ///   7. climb out of the pool west onto a platform, two more steps, finish on the goal platform
    ///
    /// Everything sits at y >= 72 so nothing is built below the start level.
    /// </summary>
    public static readonly string[] Course1 =
    [
        // --- 0. remove the lake beneath the course ---
        // There is natural water at y62 under this footprint. Baritone applies NO height limit to a fall that
        // lands in water (correct - water negates fall damage), so from almost any point on the course a drop
        // into the lake was a legal, cheap move: measured DescendWest -> (-865,62,-33) at cost 24.3 versus
        // 15.9 to continue the course. That handed the bot a free escape hatch and it took it, which read as
        // "it just jumped off". Replacing only water (terrain is left alone) makes those falls dry, and a dry
        // fall past maxFallHeightNoWater (3) is CostInf, so the course route is the only route.
        // 40 x 32 x 24 = 30,720 blocks, under the 32768 /fill limit.
        "fill -896 40 -44 -857 71 -21 minecraft:stone replace minecraft:water",

        // --- clear the working volume (y >= 72 only; ground terrain sits far below at ~y62) ---
        // 34 x 21 x 18 = 12,852 blocks, under the 32768 /fill limit.
        "fill -893 72 -41 -860 92 -24 minecraft:air",

        // --- 1. start platform (surface y72; scenario start is -863 73 -38) ---
        "fill -865 72 -40 -861 72 -36 minecraft:bedrock",

        // --- 2. ascending steps, 3 wide ---
        "fill -864 73 -35 -862 73 -35 minecraft:bedrock",
        "fill -864 74 -34 -862 74 -34 minecraft:bedrock",
        "fill -864 75 -33 -862 75 -33 minecraft:bedrock",

        // --- 3. parkour pads: 3-block gaps at a constant y75 (surface y76) ---
        "fill -864 75 -30 -862 75 -30 minecraft:bedrock",
        "fill -864 75 -27 -862 75 -27 minecraft:bedrock",

        // --- 4. walkway into the ladder tower ---
        "fill -864 75 -26 -862 75 -26 minecraft:bedrock",
        // support column the ladders hang on (south of the ladder line)
        "fill -864 76 -24 -862 88 -24 minecraft:bedrock",
        // ladders at z=-25, attached to the support at z=-24 (south), so they face north toward the bot
        "setblock -863 76 -25 minecraft:ladder[facing=north]",
        "setblock -863 77 -25 minecraft:ladder[facing=north]",
        "setblock -863 78 -25 minecraft:ladder[facing=north]",
        "setblock -863 79 -25 minecraft:ladder[facing=north]",
        "setblock -863 80 -25 minecraft:ladder[facing=north]",
        "setblock -863 81 -25 minecraft:ladder[facing=north]",
        "setblock -863 82 -25 minecraft:ladder[facing=north]",
        "setblock -863 83 -25 minecraft:ladder[facing=north]",
        "setblock -863 84 -25 minecraft:ladder[facing=north]",
        "setblock -863 85 -25 minecraft:ladder[facing=north]",
        "setblock -863 86 -25 minecraft:ladder[facing=north]",
        "setblock -863 87 -25 minecraft:ladder[facing=north]",

        // --- 5. top platform (surface y87) the bot steps onto off the ladder, plus the walkway west ---
        "fill -865 86 -29 -861 86 -26 minecraft:bedrock",
        "fill -872 86 -28 -866 86 -26 minecraft:bedrock",
        // NO gap at the ladder top. Every gap shape here was tried and none are solvable by Baritone:
        // it refuses to parkour (MovementParkour.java:93) or ascend (MovementAscend.java:115) from a
        // climbable, so it can never jump off a ladder; and a path that ENDS on top of a ladder releases its
        // inputs on success and drops (Java behaves identically - MovementPillar sets SUCCESS the instant
        // playerFeet == dest). The only ladder exit Baritone can execute is a flat traverse onto a block level
        // with the top rung, which is what this platform provides. Verified by the ladder1/ladder2 scenarios.

        // --- 6. water canal west of the walkway: an 11-block swim that cannot be walked around ---
        // Deliberately NOT a plunge pool. A pool's shell leaves a dry rim at water level, and the y87 walkway
        // is a legal 3-block fall onto that rim - the bot would drop onto it, walk around the water and step
        // onto the exit platform, skipping the swim entirely. So: the side walls run to y89 (ABOVE the
        // walkway, no rim to land on and follow), both ends are sealed, and the only way out is the far end.
        "fill -884 75 -29 -872 75 -25 minecraft:bedrock",           // floor
        "fill -884 76 -29 -872 89 -29 minecraft:bedrock",           // north wall, taller than the walkway
        "fill -884 76 -25 -872 89 -25 minecraft:bedrock",           // south wall, taller than the walkway
        "fill -872 76 -28 -872 85 -26 minecraft:bedrock",           // east end, sealed under the walkway
        "fill -884 76 -28 -884 82 -26 minecraft:bedrock",           // west end, capped by the exit platform
        "fill -883 76 -28 -873 83 -26 minecraft:water",             // water, surface y83, 11 blocks long

        // --- 7. exit platform at the far end of the swim (surface y84 = one step up out of the water) ---
        "fill -887 83 -28 -884 83 -26 minecraft:bedrock",
        "fill -888 84 -28 -888 84 -26 minecraft:bedrock",
        "fill -889 85 -28 -889 85 -26 minecraft:bedrock",
        "fill -893 86 -29 -890 86 -25 minecraft:bedrock",

        // --- 8. stone obstructions: STONE (not bedrock) so they must be mined through. Each one sits on
        //        solid footing so the bot is never asked to mine while airborne or on a ladder.
        // (a) 3 wide x 2 high wall across the walkway just before the ladder tower
        "fill -864 76 -26 -862 77 -26 minecraft:stone",
        // (b) 3 wide x 2 high wall across the high walkway between the tower and the pool
        "fill -869 87 -28 -869 88 -26 minecraft:stone",
        // (c) 3 wide x 2 high wall across the final staircase before the goal platform
        "fill -889 86 -28 -889 87 -26 minecraft:stone",
    ];

    /// <summary>
    /// "ladder1" — the minimal ladder-top test, built in the sky so nothing else can interfere.
    ///
    /// A bedrock floor at y128 (so a slip is a short, survivable drop and there is no surface water to dive
    /// into), a 12-high bedrock column, and 11 ladder rungs up its north face. There is deliberately NO
    /// platform at the top: the goal IS the block on top of the ladder, so the only way to satisfy it is to
    /// climb the last rung and stand on the ladder's top face. If the bot can do that, ladders are exitable.
    /// </summary>
    public static readonly string[] Ladder1 =
    [
        // clear + floor (15 x 23 x 13 = 4,485 blocks)
        "fill -870 128 -66 -856 150 -54 minecraft:air",
        "fill -870 128 -66 -856 128 -54 minecraft:bedrock",
        // support column the ladders hang on, at z=-59 (south of the ladder line)
        "fill -863 129 -59 -863 140 -59 minecraft:bedrock",
        // 11 rungs at z=-60, attached to the support on their south side => facing north
        "setblock -863 129 -60 minecraft:ladder[facing=north]",
        "setblock -863 130 -60 minecraft:ladder[facing=north]",
        "setblock -863 131 -60 minecraft:ladder[facing=north]",
        "setblock -863 132 -60 minecraft:ladder[facing=north]",
        "setblock -863 133 -60 minecraft:ladder[facing=north]",
        "setblock -863 134 -60 minecraft:ladder[facing=north]",
        "setblock -863 135 -60 minecraft:ladder[facing=north]",
        "setblock -863 136 -60 minecraft:ladder[facing=north]",
        "setblock -863 137 -60 minecraft:ladder[facing=north]",
        "setblock -863 138 -60 minecraft:ladder[facing=north]",
        "setblock -863 139 -60 minecraft:ladder[facing=north]",
    ];

    /// <summary>
    /// "ladder2" — the same tower, but with a platform flush with the ladder top (surface y140), i.e. the
    /// shape a normal world has. Control case: proves ordinary ladder climbing still works after any change
    /// to the climb physics.
    /// </summary>
    public static readonly string[] Ladder2 =
    [
        .. Ladder1,
        // landing platform whose SURFACE is y140, level with standing on top of the ladder
        "fill -866 139 -63 -860 139 -61 minecraft:bedrock",
    ];

    /// <summary>
    /// "tunnel1" — a solid stone wall three blocks THICK that can only be passed by mining a tunnel through
    /// it. Tests sustained multi-block breaking rather than the single-block walls in course1.
    /// </summary>
    public static readonly string[] Tunnel1 =
    [
        // Clear a WIDE buffer (z -104..-76), not just the course footprint. The retired waterup1 structure sat
        // at z -84..-76, two blocks from this course, and the bot used it to walk around the wall entirely -
        // a cheaper route than mining, and a correct choice on its part. Sky courses must not be within reach
        // of each other's geometry. 19 x 23 x 29 = 12,673 blocks, under the 32768 /fill limit.
        "fill -872 128 -104 -854 150 -76 minecraft:air",
        "fill -872 128 -94 -854 128 -86 minecraft:bedrock",
        // 3 thick (x), 4 high (y129-132), spanning the FULL width of the floor (z -94..-86).
        // Two earlier versions were both passable without tunnelling: at 3 wide on a 9-wide floor it walked
        // around the end, and at 2 high it broke a single top block and stepped over the wall. At 4 high the
        // top is +3 above the floor, which cannot be climbed without placing blocks (a jump gains +1), so the
        // only way through is a 2-high tunnel bored through all 3 layers.
        "fill -863 129 -94 -861 132 -86 minecraft:stone",
    ];

    /// <summary>
    /// "gate1" — two closed fence gates in series, each set in a bedrock wall. Confirmed against real Java
    /// Baritone: it opens fence gates. A gate is a single block, unlike a door (two halves that must be
    /// linked); an earlier version of this course placed a door via two setblocks and the halves did not
    /// behave as one door - the bot toggled the upper half open/closed forever - which tested my /setblock
    /// usage rather than the bot. Doors are worth a separate course built by placing a real door item.
    /// </summary>
    public static readonly string[] Gate1 =
    [
        "fill -872 128 -114 -854 140 -106 minecraft:air",
        "fill -872 128 -114 -854 128 -106 minecraft:bedrock",
        // Walls span the FULL width of the floor (z -114..-106), so there is no walking around them - an
        // earlier version was only 5 wide on a 9-wide floor, which left the gate optional.
        "fill -863 129 -114 -863 130 -106 minecraft:bedrock",
        "setblock -863 129 -110 minecraft:oak_fence_gate[facing=east,open=false]",
        "setblock -863 130 -110 minecraft:air",
        "fill -859 129 -114 -859 130 -106 minecraft:bedrock",
        "setblock -859 129 -110 minecraft:oak_fence_gate[facing=west,open=false]",
        "setblock -859 130 -110 minecraft:air",
    ];

    public static string[]? Get(string name) => name switch
    {
        "course1" => Course1,
        "ladder1" => Ladder1,
        "ladder2" => Ladder2,
        "tunnel1" => Tunnel1,
        "gate1" => Gate1,
        _ => null
    };
}
