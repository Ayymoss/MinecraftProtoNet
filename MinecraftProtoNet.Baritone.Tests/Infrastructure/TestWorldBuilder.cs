using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.State;

namespace MinecraftProtoNet.Baritone.Tests.Infrastructure;

/// <summary>
/// Fluent builder for creating test worlds with specific block configurations.
/// </summary>
public class TestWorldBuilder
{
    private readonly TestChunkManager _chunkManager = new();
    private readonly TestTickManager _tickManager = new();
    private readonly TestPlayerRegistry _playerRegistry = new();
    private Entity? _playerEntity;

    /// <summary>
    /// Creates a new test world builder.
    /// </summary>
    public static TestWorldBuilder Create() => new();

    /// <summary>
    /// Creates a flat floor at Y=63 (default ground level).
    /// </summary>
    public TestWorldBuilder WithFloor(int y = 63, int halfWidth = 10, string blockName = "minecraft:stone")
    {
        _chunkManager.CreateFloor(y, halfWidth, blockName);
        return this;
    }

    /// <summary>
    /// Sets a single block.
    /// </summary>
    public TestWorldBuilder WithBlock(int x, int y, int z, string blockName, bool hasCollision = true)
    {
        _chunkManager.SetBlock(x, y, z, blockName, hasCollision);
        return this;
    }

    /// <summary>
    /// Creates a wall (single block thick in X).
    /// </summary>
    public TestWorldBuilder WithWall(int x, int y1, int y2, int z1, int z2, string blockName = "minecraft:stone")
    {
        _chunkManager.Fill(x, y1, z1, x, y2, z2, blockName);
        return this;
    }

    /// <summary>
    /// Creates a pillar from ground to specified height.
    /// </summary>
    public TestWorldBuilder WithPillar(int x, int z, int baseY, int height, string blockName = "minecraft:stone")
    {
        _chunkManager.Fill(x, baseY, z, x, baseY + height - 1, z, blockName);
        return this;
    }

    /// <summary>
    /// Creates an air gap (hole in floor).
    /// </summary>
    public TestWorldBuilder WithHole(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        _chunkManager.Fill(x1, y1, z1, x2, y2, z2, "minecraft:air", false);
        return this;
    }

    /// <summary>
    /// Adds a test player at the given position.
    /// </summary>
    public TestWorldBuilder WithPlayer(double x, double y, double z, int entityId = 1)
    {
        _playerRegistry.AddEntityAsync(Guid.NewGuid(), entityId, new Vector3<double>(x, y, z)).Wait();
        var player = _playerRegistry.GetPlayerByEntityId(entityId);
        _playerEntity = player?.Entity;
        return this;
    }

    /// <summary>
    /// Builds the Level for testing.
    /// </summary>
    public Level Build()
    {
        return new Level(_tickManager, _playerRegistry, _chunkManager);
    }

    /// <summary>
    /// Builds and returns both the Level and the test player entity.
    /// </summary>
    public (Level Level, Entity Player) BuildWithPlayer()
    {
        var level = Build();
        if (_playerEntity == null)
        {
            WithPlayer(0, 64, 0);
        }
        return (level, _playerEntity!);
    }

    /// <summary>
    /// Gets the chunk manager for advanced setup.
    /// </summary>
    public TestChunkManager ChunkManager => _chunkManager;
}
