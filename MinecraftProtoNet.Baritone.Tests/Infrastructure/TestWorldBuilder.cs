using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;
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
    public TestWorldBuilder WithBlock(int x, int y, int z, string blockName, bool hasCollision = true, Dictionary<string, string>? properties = null)
    {
        int id = (blockName.EndsWith("air", StringComparison.OrdinalIgnoreCase)) ? 0 : (blockName.GetHashCode() & 0x7FFF) | 0x8000;
        var state = new BlockState(id, blockName)
        {
            HasCollision = hasCollision
        };
        if (properties != null)
        {
            foreach (var kvp in properties) state.Properties[kvp.Key] = kvp.Value;
        }
        _chunkManager.SetBlock(x, y, z, state);
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

    // ===== Terrain Helpers =====

    /// <summary>
    /// Creates a staircase going in a direction.
    /// </summary>
    /// <param name="startX">Starting X coordinate</param>
    /// <param name="startY">Starting Y coordinate (floor level)</param>
    /// <param name="startZ">Starting Z coordinate</param>
    /// <param name="steps">Number of steps</param>
    /// <param name="dirX">Direction X (-1, 0, or 1)</param>
    /// <param name="dirZ">Direction Z (-1, 0, or 1)</param>
    /// <param name="blockName">Block to use for stairs</param>
    public TestWorldBuilder WithStairs(int startX, int startY, int startZ, int steps, int dirX, int dirZ, string blockName = "minecraft:stone")
    {
        for (int i = 0; i < steps; i++)
        {
            var x = startX + i * dirX;
            var y = startY + i + 1;
            var z = startZ + i * dirZ;
            _chunkManager.SetBlock(x, y, z, blockName);
        }
        return this;
    }

    /// <summary>
    /// Creates a water pool.
    /// </summary>
    public TestWorldBuilder WithPool(int x1, int z1, int x2, int z2, int y, int depth = 2)
    {
        // Remove floor where pool is
        for (int x = x1; x <= x2; x++)
        for (int z = z1; z <= z2; z++)
        for (int dy = 0; dy < depth; dy++)
        {
            var waterState = new BlockState(GetIdFromName("minecraft:water"), "minecraft:water")
            {
                HasCollision = false
            };
            _chunkManager.SetBlock(x, y - dy, z, waterState);
        }

        // Pool floor
        _chunkManager.Fill(x1, y - depth, z1, x2, y - depth, z2, "minecraft:stone");
        return this;
    }

    /// <summary>
    /// Creates a gap (hole) in the floor that can be jumped over.
    /// </summary>
    public TestWorldBuilder WithGap(int x, int y, int z, int width, int dirX, int dirZ)
    {
        for (int i = 0; i < width; i++)
        {
            var gapX = x + i * dirX;
            var gapZ = z + i * dirZ;
            _chunkManager.SetBlock(gapX, y, gapZ, "minecraft:air", false);
        }
        return this;
    }

    /// <summary>
    /// Creates a ladder column.
    /// </summary>
    public TestWorldBuilder WithLadder(int x, int y, int z, int height)
    {
        for (int i = 0; i < height; i++)
        {
            var ladderState = new BlockState(GetIdFromName("minecraft:ladder"), "minecraft:ladder")
            {
                HasCollision = false
            };
            _chunkManager.SetBlock(x, y + i, z, ladderState);
        }
        return this;
    }

    /// <summary>
    /// Creates a raised platform.
    /// </summary>
    public TestWorldBuilder WithPlatform(int x1, int z1, int x2, int z2, int y, string blockName = "minecraft:stone")
    {
        _chunkManager.Fill(x1, y, z1, x2, y, z2, blockName);
        return this;
    }

    /// <summary>
    /// Creates a spiral staircase.
    /// </summary>
    public TestWorldBuilder WithSpiralStairs(int centerX, int centerZ, int baseY, int floors, string blockName = "minecraft:stone")
    {
        int[] dxs = { 1, 0, -1, 0 };
        int[] dzs = { 0, 1, 0, -1 };
        int stepsPerFloor = 4;

        for (int floor = 0; floor < floors; floor++)
        {
            for (int step = 0; step < stepsPerFloor; step++)
            {
                var dir = (floor * stepsPerFloor + step) % 4;
                var x = centerX + dxs[dir];
                var z = centerZ + dzs[dir];
                var y = baseY + floor * stepsPerFloor + step + 1;
                _chunkManager.SetBlock(x, y, z, blockName);
            }
        }
        return this;
    }

    private static int GetIdFromName(string name) => name switch
    {
        "minecraft:air" => 0,
        "minecraft:stone" => 1,
        "minecraft:water" => 5,
        "minecraft:ladder" => 100,
        _ => (name.GetHashCode() & 0x7FFF) | 0x8000
    };
}
