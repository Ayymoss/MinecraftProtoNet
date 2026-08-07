using FluentAssertions;
using MinecraftProtoNet.Core.Physics;
using MinecraftProtoNet.Core.Physics.Shapes;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Tests.Core.Physics;

/// <summary>
/// Stair collision shapes, which had two independent faults.
///
/// The step was placed on the side OPPOSITE the one the stair faces — vanilla's SHAPE_OUTER is
/// Block.box(0,8,0, 8,16,8) (the north-west quarter) and Shapes.rotateHorizontal keys the unrotated shape as
/// NORTH, so a north-facing stair's step is on its north side. And the `shape` property was ignored outright,
/// so outer corners got a full half-strip where vanilla has a quarter, letting the bot stand half a block
/// higher than vanilla permits.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/level/block/StairBlock.java:63-101, 232-240
/// </summary>
public sealed class StairShapeTests : IClassFixture<RegistryFixture>
{
    private static VoxelShape Shape(string facing, string half, string shape)
    {
        var id = RegistryFixture.StateId("minecraft:stone_brick_stairs",
            ("facing", facing), ("half", half), ("shape", shape), ("waterlogged", "false"));
        return BlockShapeRegistry.Shared.GetShape(ClientState.BlockStateRegistry[id]);
    }

    private static bool Covers(VoxelShape shape, double x, double y, double z)
        => shape.ToAABBs().Any(b =>
            x > b.MinX && x < b.MaxX && y > b.MinY && y < b.MaxY && z > b.MinZ && z < b.MaxZ);

    // Quarter-column sample points in the upper half of a bottom stair. North is -Z, west is -X.
    private const double Up = 0.75;
    private static readonly (double X, double Z) NorthWest = (0.25, 0.25);
    private static readonly (double X, double Z) NorthEast = (0.75, 0.25);
    private static readonly (double X, double Z) SouthWest = (0.25, 0.75);
    private static readonly (double X, double Z) SouthEast = (0.75, 0.75);

    [Fact]
    public void BottomStair_AlwaysHasASolidLowerHalf()
    {
        var shape = Shape("north", "bottom", "straight");
        Covers(shape, 0.5, 0.25, 0.5).Should().BeTrue();
    }

    [Fact]
    public void StraightStair_PutsTheStepOnTheSideItFaces()
    {
        var shape = Shape("north", "bottom", "straight");

        Covers(shape, NorthWest.X, Up, NorthWest.Z).Should().BeTrue("a north-facing stair steps up on its north side");
        Covers(shape, NorthEast.X, Up, NorthEast.Z).Should().BeTrue();
        Covers(shape, SouthWest.X, Up, SouthWest.Z).Should().BeFalse("the south half stays open");
        Covers(shape, SouthEast.X, Up, SouthEast.Z).Should().BeFalse();
    }

    [Theory]
    [InlineData("south", 0.5, 0.75)]
    [InlineData("west", 0.25, 0.5)]
    [InlineData("east", 0.75, 0.5)]
    public void StraightStair_StepFollowsFacingForEveryDirection(string facing, double stepX, double stepZ)
    {
        var shape = Shape(facing, "bottom", "straight");
        Covers(shape, stepX, Up, stepZ).Should().BeTrue();

        // ... and the opposite half is open.
        Covers(shape, 1.0 - stepX, Up, 1.0 - stepZ).Should().BeFalse();
    }

    [Fact]
    public void OuterStair_CoversOnlyOneQuarter()
    {
        // outer_left of a north-facing stair keys off counter-clockwise (west), giving the north-west quarter.
        var left = Shape("north", "bottom", "outer_left");
        Covers(left, NorthWest.X, Up, NorthWest.Z).Should().BeTrue();
        Covers(left, NorthEast.X, Up, NorthEast.Z).Should().BeFalse("an outer corner is a quarter, not a half");
        Covers(left, SouthWest.X, Up, SouthWest.Z).Should().BeFalse();
        Covers(left, SouthEast.X, Up, SouthEast.Z).Should().BeFalse();

        // outer_right mirrors it, keying off clockwise (east).
        var right = Shape("north", "bottom", "outer_right");
        Covers(right, NorthEast.X, Up, NorthEast.Z).Should().BeTrue();
        Covers(right, NorthWest.X, Up, NorthWest.Z).Should().BeFalse();
    }

    [Fact]
    public void InnerStair_CoversThreeQuarters()
    {
        // inner_right of a north-facing stair adds the clockwise (east) half, leaving only south-west open.
        var right = Shape("north", "bottom", "inner_right");
        Covers(right, NorthWest.X, Up, NorthWest.Z).Should().BeTrue();
        Covers(right, NorthEast.X, Up, NorthEast.Z).Should().BeTrue();
        Covers(right, SouthEast.X, Up, SouthEast.Z).Should().BeTrue();
        Covers(right, SouthWest.X, Up, SouthWest.Z).Should().BeFalse("an inner corner leaves one quarter open");

        // inner_left adds the counter-clockwise (west) half instead, leaving south-east open.
        var left = Shape("north", "bottom", "inner_left");
        Covers(left, SouthWest.X, Up, SouthWest.Z).Should().BeTrue();
        Covers(left, SouthEast.X, Up, SouthEast.Z).Should().BeFalse();
    }

    [Fact]
    public void TopStair_MirrorsVertically()
    {
        var shape = Shape("north", "top", "straight");

        // Upper half solid throughout. Sampled either side of the z=0.5 seam rather than on it: the shape
        // merges into a full-height north box and an upper-half south box, and a point exactly on the seam
        // lies strictly inside neither.
        Covers(shape, 0.5, 0.75, 0.25).Should().BeTrue();
        Covers(shape, 0.5, 0.75, 0.75).Should().BeTrue();
        Covers(shape, NorthWest.X, 0.25, NorthWest.Z).Should().BeTrue();
        Covers(shape, SouthWest.X, 0.25, SouthWest.Z).Should().BeFalse();
    }
}
