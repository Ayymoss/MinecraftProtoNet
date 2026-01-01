namespace MinecraftProtoNet.Pathfinding.Movement;

/// <summary>
/// Represents a movement direction with its coordinate offsets.
/// Based on Baritone's Moves enum, but simplified for the basic movements.
/// </summary>
public class MoveDirection
{
    public string Name { get; }
    public int XOffset { get; }
    public int YOffset { get; }
    public int ZOffset { get; }

    /// <summary>
    /// Whether the X/Z can vary (e.g., parkour with variable distance).
    /// </summary>
    public bool DynamicXZ { get; }

    /// <summary>
    /// Whether the Y can vary (e.g., descend with variable fall distance).
    /// </summary>
    public bool DynamicY { get; }

    private MoveDirection(string name, int xOffset, int yOffset, int zOffset, bool dynamicXZ = false, bool dynamicY = false)
    {
        Name = name;
        XOffset = xOffset;
        YOffset = yOffset;
        ZOffset = zOffset;
        DynamicXZ = dynamicXZ;
        DynamicY = dynamicY;
    }

    // ===== Basic Traversal (flat, 4 directions) =====
    public static readonly MoveDirection TraverseNorth = new("TraverseNorth", 0, 0, -1);
    public static readonly MoveDirection TraverseSouth = new("TraverseSouth", 0, 0, 1);
    public static readonly MoveDirection TraverseEast = new("TraverseEast", 1, 0, 0);
    public static readonly MoveDirection TraverseWest = new("TraverseWest", -1, 0, 0);

    // ===== Ascend (jump up 1 block, 4 directions) =====
    public static readonly MoveDirection AscendNorth = new("AscendNorth", 0, 1, -1);
    public static readonly MoveDirection AscendSouth = new("AscendSouth", 0, 1, 1);
    public static readonly MoveDirection AscendEast = new("AscendEast", 1, 1, 0);
    public static readonly MoveDirection AscendWest = new("AscendWest", -1, 1, 0);

    // ===== Descend (drop 1 block, 4 directions, dynamic Y for falls) =====
    public static readonly MoveDirection DescendNorth = new("DescendNorth", 0, -1, -1, dynamicY: true);
    public static readonly MoveDirection DescendSouth = new("DescendSouth", 0, -1, 1, dynamicY: true);
    public static readonly MoveDirection DescendEast = new("DescendEast", 1, -1, 0, dynamicY: true);
    public static readonly MoveDirection DescendWest = new("DescendWest", -1, -1, 0, dynamicY: true);

    // ===== Diagonal (4 corners, dynamic Y for terrain) =====
    public static readonly MoveDirection DiagonalNE = new("DiagonalNE", 1, 0, -1, dynamicY: true);
    public static readonly MoveDirection DiagonalNW = new("DiagonalNW", -1, 0, -1, dynamicY: true);
    public static readonly MoveDirection DiagonalSE = new("DiagonalSE", 1, 0, 1, dynamicY: true);
    public static readonly MoveDirection DiagonalSW = new("DiagonalSW", -1, 0, 1, dynamicY: true);

    // ===== Vertical =====
    public static readonly MoveDirection Pillar = new("Pillar", 0, 1, 0);
    public static readonly MoveDirection Downward = new("Downward", 0, -1, 0);

    // ===== Parkour (4 directions, dynamic X/Z for variable jump distance) =====
    public static readonly MoveDirection ParkourNorth = new("ParkourNorth", 0, 0, -4, dynamicXZ: true, dynamicY: true);
    public static readonly MoveDirection ParkourSouth = new("ParkourSouth", 0, 0, 4, dynamicXZ: true, dynamicY: true);
    public static readonly MoveDirection ParkourEast = new("ParkourEast", 4, 0, 0, dynamicXZ: true, dynamicY: true);
    public static readonly MoveDirection ParkourWest = new("ParkourWest", -4, 0, 0, dynamicXZ: true, dynamicY: true);

    /// <summary>
    /// All basic movement directions (excluding parkour for initial implementation).
    /// </summary>
    public static readonly MoveDirection[] BasicMoves =
    [
        // Traverse
        TraverseNorth, TraverseSouth, TraverseEast, TraverseWest,
        // Ascend
        AscendNorth, AscendSouth, AscendEast, AscendWest,
        // Descend
        DescendNorth, DescendSouth, DescendEast, DescendWest,
        // Diagonal
        DiagonalNE, DiagonalNW, DiagonalSE, DiagonalSW,
        // Vertical
        Pillar, Downward
    ];

    /// <summary>
    /// All movement directions including parkour.
    /// </summary>
    public static readonly MoveDirection[] AllMoves =
    [
        ..BasicMoves,
        // Parkour
        ParkourNorth, ParkourSouth, ParkourEast, ParkourWest
    ];

    public override string ToString() => Name;
}
