using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State;
using Moq;

namespace MinecraftProtoNet.Tests.Core.Physics;

/// <summary>
/// Regression tests for the geometry that produced every setback on the Hypixel SkyBlock hub route: a
/// staircase of half-steps where a full block (top 77.0) meets a bottom slab (top 76.5).
///
/// The bug these pin down is an ordering one. Vanilla resolves collision Y-axis first
/// (Direction.axisStepOrder, Direction.java:430-432), which means Y is resolved from the PRE-movement
/// horizontal position. An entity walking off a ledge is therefore still supported by the block it is
/// standing on for that tick — the downward movement clips to zero and the fall only begins on the following
/// tick. Resolving X first moves the box out over the drop before Y is tested, which starts the fall a tick
/// early and leaves the entity 0.0784 low: exactly enough to miss the slab top that vanilla lands on and to
/// fall through to the next step down.
///
/// GrimAC, simulating vanilla independently, reported this as a Simulation offset of exactly .078400 (one
/// gravity tick) together with a GroundSpoof "claimed false".
/// </summary>
public sealed class LedgeDescentTests : IClassFixture<RegistryFixture>
{
    // Real ids from the live world, read back off the server at (-5,76,-4) during the recon runs.
    private static readonly int StoneBricks = RegistryFixture.StateId("minecraft:stone_bricks");
    private static readonly int BottomSlab = RegistryFixture.StateId(
        "minecraft:stone_brick_slab", ("type", "bottom"), ("waterlogged", "false"));

    private const double FullBlockTop = 77.0;
    private const double SlabTop = 76.5;

    /// <summary>
    /// Full blocks for x >= -4 (surface 77.0) meeting bottom slabs for x &lt;= -5 (surface 76.5), spanning
    /// enough Z either side that the 0.6-wide player box is always fully supported across the seam.
    /// </summary>
    private static (Level Level, Entity Player) BuildLedge()
    {
        var chunks = new TestChunkManager();

        for (var z = -8; z <= 0; z++)
        {
            for (var x = -4; x <= 2; x++)
            {
                chunks.SetColumn(x, z, 70, 76, StoneBricks); // top face at y = 77.0
            }

            for (var x = -12; x <= -5; x++)
            {
                chunks.SetColumn(x, z, 70, 75, StoneBricks); // solid to y = 76.0 ...
                chunks.Set(x, 76, z, BottomSlab);            // ... capped by a bottom slab, top face 76.5
            }
        }

        // The entity-push pass walks the player registry every tick, so it has to return a real (empty)
        // collection rather than Moq's default null.
        var players = new Mock<IPlayerRegistry>();
        players.Setup(p => p.GetAllPlayers()).Returns(Array.Empty<Player>());

        var level = new Level(Mock.Of<ITickManager>(), players.Object, chunks);
        var player = new Entity
        {
            EntityId = 1,
            Position = new Vector3<double>(-3.9, FullBlockTop, -3.5),
            IsOnGround = true,
            MovementSpeed = 0.1673, // the SkyBlock speed stat the live runs used

            // A standing entity is never at rest vertically: landing zeroes velocity.y, then travel applies
            // gravity for the next tick, so it always enters a tick at -0.0784 and re-collides with the floor.
            // Starting from 0 would mean the first tick makes no downward probe and reports IsOnGround=false.
            Velocity = new Vector3<double>(0.0, -0.0784, 0.0)
        };

        return (level, player);
    }

    private static PhysicsService NewPhysics()
        => new(NullLogger<PhysicsService>.Instance, new DeterministicHumanizer());

    /// <summary>
    /// Holds horizontal velocity constant so the test isolates collision resolution from input handling and
    /// friction. Y is left alone to evolve under gravity.
    /// </summary>
    private static Action<Entity> HoldVelocityX(double vx)
        => e => e.Velocity = new Vector3<double>(vx, e.Velocity.Y, 0.0);

    [Fact]
    public async Task WalkingOffALedge_StaysGroundedForTheTickItStepsOff()
    {
        var (level, player) = BuildLedge();
        var physics = NewPhysics();
        var sender = new NullPacketSender();
        var hold = HoldVelocityX(-0.3);

        // Tick 1: box spans -4.2..-3.6, still overlapping the full-block column at x = -4.
        await physics.PhysicsTickAsync(player, level, sender, hold);
        player.Position.Y.Should().Be(FullBlockTop);
        player.IsOnGround.Should().BeTrue();

        // Tick 2: box spans -4.5..-3.9. Its max edge is still inside the full-block column, so vanilla keeps
        // the entity supported. This is the assertion that fails when X is resolved before Y — the box gets
        // moved clear of the ledge first and then falls a tick early.
        await physics.PhysicsTickAsync(player, level, sender, hold);
        player.Position.Y.Should().Be(FullBlockTop);
        player.IsOnGround.Should().BeTrue();

        // Tick 3: now fully over the slab column, so the fall begins — one gravity tick, -0.0784.
        await physics.PhysicsTickAsync(player, level, sender, hold);
        player.IsOnGround.Should().BeFalse();
        // Tolerance is 1e-6 rather than exact: the gravity constant is single-precision, so the first fall
        // step lands on 76.92159999847412 rather than a clean 76.9216.
        player.Position.Y.Should().BeApproximately(FullBlockTop - 0.0784, 1e-6);
    }

    [Fact]
    public async Task DescendingOntoASlab_LandsOnTheSlabTopAndNeverFallsThrough()
    {
        var (level, player) = BuildLedge();
        var physics = NewPhysics();
        var sender = new NullPacketSender();
        var hold = HoldVelocityX(-0.15);

        var lowest = player.Position.Y;
        for (var tick = 0; tick < 20; tick++)
        {
            await physics.PhysicsTickAsync(player, level, sender, hold);
            lowest = Math.Min(lowest, player.Position.Y);
        }

        lowest.Should().BeGreaterThanOrEqualTo(SlabTop,
            "the bottom slab's top face is solid ground — dropping below it is the fall-through that produced "
            + "every setback on the live route");
        player.Position.Y.Should().Be(SlabTop);
        player.IsOnGround.Should().BeTrue();
    }

    [Fact]
    public async Task WalkingOnFlatGround_NeverLeavesTheSurface()
    {
        // Guards the inverse failure: resolving Y first must not make the entity sticky on level ground.
        var (level, player) = BuildLedge();
        var physics = NewPhysics();
        var sender = new NullPacketSender();
        var hold = HoldVelocityX(0.25); // +X, away from the ledge, staying on full blocks

        for (var tick = 0; tick < 10; tick++)
        {
            await physics.PhysicsTickAsync(player, level, sender, hold);
            player.Position.Y.Should().Be(FullBlockTop);
            player.IsOnGround.Should().BeTrue();
        }
    }
}
