using System.Collections.Concurrent;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Models.World.Meta;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Physics;
using MinecraftProtoNet.Core.Physics.Shapes;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Tests.Core.Physics;

/// <summary>
/// A hand-built world for physics tests: blocks are placed by world coordinate into a dictionary rather than
/// decoded from chunk packets, so a piece of terrain can be described in a few lines.
///
/// <see cref="GetCollidingShapes"/> deliberately mirrors <c>ChunkManager.GetCollidingShapes</c> line for line,
/// including the air/liquid skip and the per-block <c>Move(x, y, z)</c>. If the two ever diverge these tests
/// stop saying anything about the real client, so keep them in step.
/// </summary>
public sealed class TestChunkManager : IChunkManager
{
    private readonly Dictionary<(int X, int Y, int Z), int> _blocks = new();
    private readonly BlockShapeRegistry _shapes = BlockShapeRegistry.Shared;

    public ConcurrentDictionary<(int ChunkX, int ChunkZ), Chunk> Chunks { get; } = new();

    public void Set(int x, int y, int z, int blockStateId) => _blocks[(x, y, z)] = blockStateId;

    /// <summary>Fills a column of full blocks from <paramref name="fromY"/> up to and including <paramref name="toY"/>.</summary>
    public void SetColumn(int x, int z, int fromY, int toY, int blockStateId)
    {
        for (var y = fromY; y <= toY; y++) Set(x, y, z, blockStateId);
    }

    public BlockState? GetBlockAt(int worldX, int worldY, int worldZ)
        => _blocks.TryGetValue((worldX, worldY, worldZ), out var id)
            ? ClientState.BlockStateRegistry.GetValueOrDefault(id)
            : ClientState.BlockStateRegistry.GetValueOrDefault(0); // unset == air, as an unloaded-free world

    public IEnumerable<VoxelShape> GetCollidingShapes(AABB queryBox)
    {
        var minBx = (int)Math.Floor(queryBox.MinX);
        var minBy = (int)Math.Floor(queryBox.MinY);
        var minBz = (int)Math.Floor(queryBox.MinZ);
        var maxBx = (int)Math.Floor(queryBox.MaxX);
        var maxBy = (int)Math.Floor(queryBox.MaxY);
        var maxBz = (int)Math.Floor(queryBox.MaxZ);

        for (var y = minBy; y <= maxBy; y++)
        {
            if (y is < -64 or > 319) continue;

            for (var x = minBx; x <= maxBx; x++)
            for (var z = minBz; z <= maxBz; z++)
            {
                var blockState = GetBlockAt(x, y, z);
                if (blockState == null || blockState.IsAir || blockState.IsLiquid) continue;

                var shape = _shapes.GetShape(blockState);
                if (!shape.IsEmpty()) yield return shape.Move(x, y, z);
            }
        }
    }

    public List<AABB> GetCollidingBlockAABBs(AABB queryBox)
    {
        var list = new List<AABB>();
        foreach (var shape in GetCollidingShapes(queryBox))
        foreach (var box in shape.ToAABBs())
            if (box.Intersects(queryBox))
                list.Add(box);
        return list;
    }

    public void HandleBlockUpdate(Vector3<double> position, int blockStateId)
        => Set((int)Math.Floor(position.X), (int)Math.Floor(position.Y), (int)Math.Floor(position.Z), blockStateId);

    public RaycastHit? RayCast(Vector3<double> start, Vector3<double> direction, double maxDistance = 100.0) => null;
    public void AddChunk(Chunk chunk) { }
    public bool HasChunk(int chunkX, int chunkZ) => true;
    public Chunk? GetChunk(int chunkX, int chunkZ) => null;
    public void SetBlockEntity(Vector3<int> position, int type, NbtTag? nbt) { }
    public NbtTag? GetBlockEntity(Vector3<int> position) => null;
}

/// <summary>No-op sender: these tests exercise simulation, not the wire.</summary>
public sealed class NullPacketSender : IPacketSender
{
    public Task SendPacketAsync(IServerboundPacket packet, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>Humanizer stub with every source of randomness set to zero, so runs are byte-identical.</summary>
public sealed class DeterministicHumanizer : IHumanizer
{
    public bool IsEnabled => false;
    public bool IsRemoteServer => false;
    public int GetTickJitterMs() => 0;
    public (float yawOffset, float pitchOffset) GetRotationJitter() => (0f, 0f);
    public int GetGuiClickDelayMs() => 0;
    public int GetGuiNavigationDelayMs() => 0;
    public int GetChatCommandDelayMs() => 0;
    public int GetPostActionDelayMs() => 0;
    public bool ShouldPerformIdleAction() => false;
    public int GetIdleLookSlewTicks() => 1;

    public (float yaw, float pitch)? GetIdleLookTarget(
        Vector3<double> playerPosition,
        Vector2<float> currentYawPitch,
        WorldEntityRegistry worldEntities) => null;
}

/// <summary>
/// Loads the real block registries once per test run. The tests use genuine block-state ids taken from the
/// live world (see LedgeDescentTests) rather than synthetic ones, so the collision shapes under test are
/// exactly the shapes the client uses in play.
/// </summary>
public sealed class RegistryFixture
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _loaded;

    public RegistryFixture()
    {
        Gate.Wait();
        try
        {
            if (_loaded) return;
            var loader = new RegistryDataLoader();
            ClientState.InitializeBlockStateRegistry(loader.LoadBlockStatesAsync().GetAwaiter().GetResult());
            ClientState.InitializeBlockDefinitions(loader.LoadBlockDefinitionsAsync().GetAwaiter().GetResult());
            ClientState.InitializeBlockHardness(loader.LoadBlockHardnessAsync().GetAwaiter().GetResult());
            ClientState.InitializeBlockTags();
            _loaded = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Resolves a block-state id by block name and exact property set, so tests read declaratively.</summary>
    public static int StateId(string blockName, params (string Key, string Value)[] properties)
    {
        foreach (var (id, state) in ClientState.BlockStateRegistry)
        {
            if (!string.Equals(state.Name, blockName, StringComparison.OrdinalIgnoreCase)) continue;
            var all = properties.All(p =>
                state.Properties != null &&
                state.Properties.TryGetValue(p.Key, out var v) &&
                string.Equals(v, p.Value, StringComparison.OrdinalIgnoreCase));
            if (all) return id;
        }

        throw new InvalidOperationException($"No block state for {blockName} with the requested properties.");
    }
}
