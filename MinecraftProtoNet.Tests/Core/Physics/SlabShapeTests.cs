using FluentAssertions;
using MinecraftProtoNet.Core.Physics;
using MinecraftProtoNet.Core.Physics.Shapes;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Tests.Core.Physics;

/// <summary>
/// Slab collision shapes, pinned because a slab is where our position diverges from the server's.
///
/// Measured on Hypixel 2026-08-09: per unit of movement we take roughly ten times the server position
/// corrections a vanilla client does (3.46 per 100 movement packets against 0.36 for a vanilla client
/// running scripted strafes). A bot-vs-bot capture had already localised one case to a slab at
/// (-5,76,-4), where Java settles at y=76.500 — the top of a bottom slab — and we continue to 76.000.
///
/// Landing at 76.000 means the shape resolved EMPTY, not merely wrong, so these tests check the two ways
/// that can happen independently of the box arithmetic: the shape itself, and the tag lookup that
/// BlockState.IsSlab depends on. IsSlab reads a TAG (`ClientState.BlockTags.HasTag(name, "slabs")`), so a
/// missing or unloaded tag set silently reclassifies every slab in the world.
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/level/block/SlabBlock.java
/// </summary>
public sealed class SlabShapeTests : IClassFixture<RegistryFixture>
{
    private static VoxelShape Shape(string type)
    {
        var id = RegistryFixture.StateId("minecraft:stone_brick_slab",
            ("type", type), ("waterlogged", "false"));
        return BlockShapeRegistry.Shared.GetShape(ClientState.BlockStateRegistry[id]);
    }

    private static double TopOf(VoxelShape shape) =>
        shape.ToAABBs().Count == 0 ? double.NaN : shape.ToAABBs().Max(b => b.MaxY);

    [Fact]
    public void BottomSlab_IsNotEmpty()
    {
        // The failure that matters. An empty shape is not "a slightly wrong box", it is a hole in the floor:
        // the player passes through and every subsequent tick disagrees with the server until it teleports us.
        Shape("bottom").ToAABBs().Should().NotBeEmpty("a bottom slab must have collision, or we fall through it");
    }

    [Fact]
    public void BottomSlab_TopSurfaceIsAtHalfHeight()
        => TopOf(Shape("bottom")).Should().Be(0.5, "vanilla settles a player on a bottom slab at y+0.5");

    [Fact]
    public void TopSlab_OccupiesTheUpperHalf()
    {
        var boxes = Shape("top").ToAABBs();
        boxes.Should().NotBeEmpty();
        boxes.Min(b => b.MinY).Should().Be(0.5);
        TopOf(Shape("top")).Should().Be(1.0);
    }

    [Fact]
    public void DoubleSlab_IsAFullCube()
        => TopOf(Shape("double")).Should().Be(1.0);

    /// <summary>
    /// The tag lookup is the single point of failure behind the whole shape. If it answers false the block
    /// never reaches the slab branch at all, so this is asserted directly rather than inferred from a box.
    /// </summary>
    [Fact]
    public void ASlab_IsRecognisedAsASlab()
    {
        var id = RegistryFixture.StateId("minecraft:stone_brick_slab",
            ("type", "bottom"), ("waterlogged", "false"));
        ClientState.BlockStateRegistry[id].IsSlab
            .Should().BeTrue("IsSlab drives the shape; if the slabs tag is missing, every slab loses its shape");
    }
}
